' User Lists Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class MyListsController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 0

    Protected model As UserLists

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of UserLists)()
        model = model0

        'initialization
        base_url = "/My/Lists"
        required_fields = "entity iname"
        save_fields = "entity iname idesc status"

        search_fields = "iname idesc"
        list_sortdef = "iname asc"   'default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time")

        related_id = reqs("related_id")
    End Sub

    Public Overrides Function initFilter(Optional session_key As String = Nothing) As Hashtable
        Dim result = MyBase.initFilter(session_key)
        If Not Me.list_filter.ContainsKey("entity") Then
            Me.list_filter("entity") = related_id
        End If
        Return Me.list_filter
    End Function

    Public Overrides Sub setListSearch()
        list_where = " status<>127 and add_users_id = " & db.qi(fw.model(Of Users).meId) 'only logged user lists

        MyBase.setListSearch()

        If list_filter("entity") > "" Then
            Me.list_where &= " and entity=" & db.q(list_filter("entity"))
        End If
    End Sub
    Public Overrides Sub getListRows()
        MyBase.getListRows()

        For Each row As Hashtable In Me.list_rows
            row("ctr") = model.countItems(row("id"))
        Next
    End Sub

    Public Overrides Function ShowFormAction(Optional form_id As String = "") As Hashtable
        Me.form_new_defaults = New Hashtable
        Me.form_new_defaults("entity") = related_id
        Return MyBase.ShowFormAction(form_id)
    End Function

    Public Overrides Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in Controller.save_fields")

        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)
        Dim success = True
        Dim is_new = (id = 0)

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim item_old As Hashtable = model0.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Me.save_fields)
            If Me.save_fields_checkboxes > "" Then FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes)

            id = Me.modelAddOrUpdate(id, itemdb)

            If is_new AndAlso item.ContainsKey("item_id") Then
                'item_id could contain comma-separated ids
                Dim hids = Utils.commastr2hash(item("item_id"))
                If hids.Count > 0 Then
                    'if item id passed - link item with the created list
                    For Each sitem_id As String In hids.Keys
                        Dim item_id = Utils.f2int(sitem_id)
                        If item_id > 0 Then model.addItems(id, item_id)
                    Next
                End If
            End If

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        If return_url > "" Then
            fw.redirect(return_url)
        End If

        Return Me.afterSave(success, id, is_new)
    End Function

    Public Function ToggleListAction(form_id As String) As Hashtable
        Dim user_lists_id = Utils.f2int(form_id)
        Dim item_id = reqi("item_id")
        Dim ps = New Hashtable From {
                {"_json", True},
                {"success", True}
            }

        Try
            Dim user_lists = fw.model(Of UserLists).one(user_lists_id)
            If item_id = 0 OrElse user_lists.Count = 0 OrElse user_lists("add_users_id") <> fw.model(Of Users).meId Then Throw New ApplicationException("Wrong Request")

            Dim res = fw.model(Of UserLists).toggleItemList(user_lists_id, item_id)
            ps("iname") = user_lists("iname")
            ps("action") = IIf(res, "added", "removed")

        Catch ex As ApplicationException
            ps("success") = False
            ps("err_msg") = ex.Message
        End Try

        Return ps
    End Function

    'request item_id - could be one id, or comma-separated ids
    Public Function AddToListAction(form_id As String) As Hashtable
        Dim user_lists_id = Utils.f2int(form_id)
        Dim items As Hashtable = Utils.commastr2hash(reqs("item_id"))

        Dim ps = New Hashtable From {
                {"_json", True},
                {"success", True}
            }

        Try
            Dim user_lists = fw.model(Of UserLists).one(user_lists_id)
            If user_lists.Count = 0 OrElse user_lists("add_users_id") <> fw.model(Of Users).meId Then Throw New ApplicationException("Wrong Request")

            For Each key As String In items.Keys
                Dim item_id = Utils.f2int(key)
                If item_id > 0 Then
                    fw.model(Of UserLists).addItemList(user_lists_id, item_id)
                End If
            Next

        Catch ex As ApplicationException
            ps("success") = False
            ps("err_msg") = ex.Message
        End Try

        Return ps
    End Function

    'request item_id - could be one id, or comma-separated ids
    Public Function RemoveFromListAction(form_id As String) As Hashtable
        Dim user_lists_id = Utils.f2int(form_id)
        Dim items As Hashtable = Utils.commastr2hash(reqs("item_id"))
        Dim ps = New Hashtable From {
                {"_json", True},
                {"success", True}
            }

        Try
            Dim user_lists = fw.model(Of UserLists).one(user_lists_id)
            If user_lists.Count = 0 OrElse user_lists("add_users_id") <> fw.model(Of Users).meId Then Throw New ApplicationException("Wrong Request")

            For Each key As String In items.Keys
                Dim item_id = Utils.f2int(key)
                If item_id > 0 Then
                    fw.model(Of UserLists).delItemList(user_lists_id, item_id)
                End If
            Next

        Catch ex As ApplicationException
            ps("success") = False
            ps("err_msg") = ex.Message
        End Try

        Return ps
    End Function

End Class
