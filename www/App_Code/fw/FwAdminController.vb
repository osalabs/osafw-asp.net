' Base Admin screens controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2017 Oleg Savchuk www.osalabs.com

Public Class FwAdminController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100
    'Public Shared Shadows route_default_action As String = "index" 'empty|index|show - calls IndexAction or ShowAction accordingly if no requested controller action found. If empty (default) - show template from /cur_controller/cur_action dir

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'DEFINE in inherited controllers like this:
        'base_url = "/Admin/Base"
        'base_url_suffix = "?parent_id=123"
        'required_fields = "iname"
        'save_fields = "iname idesc status"
        'save_fields_checkboxes = ""
        'save_fields_nullable=""

        'search_fields = "iname idesc"
        'list_sortdef = "iname asc"
        'list_sortmap = Utils.qh("id|id iname|iname add_time|add_time")

        'list_view = model0.table_name 'optionally override list view/table
    End Sub

    Public Overridable Function IndexAction() As Hashtable
        'get filters from the search form
        Dim f As Hashtable = Me.initFilter()

        Me.setListSorting()

        Me.setListSearch()
        Me.setListSearchStatus() 'status field is not always in table, so keep it separate
        'set here non-standard search
        'If f("field") > "" Then
        '    Me.list_where &= " and field=" & db.q(f("field"))
        'End If

        Me.getListRows()
        'add/modify rows from db if necessary
        'For Each row As Hashtable In Me.list_rows
        '    row("field") = "value"
        'Next

        'set standard output parse strings
        Dim ps = Me.setPS()

        'userlists support if necessary
        If Me.is_userlists Then Me.setUserLists(ps)

        Return ps
    End Function

    Public Overridable Function ShowAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)
        Dim item As Hashtable = model0.one(id)
        If item.Count = 0 Then Throw New ApplicationException("Not Found")

        setAddUpdUser(ps, item)

        'userlists support if necessary
        If Me.is_userlists Then Me.setUserLists(ps, id)

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id
        ps("base_url") = base_url
        ps("is_userlists") = is_userlists

        Return ps
    End Function

    ''' <summary>
    ''' Shows editable Form for adding or editing one entity row
    ''' </summary>
    ''' <param name="form_id"></param>
    ''' <returns>in Hashtable:
    ''' id - id of the entity
    ''' i - hashtable of entity fields
    ''' </returns>
    ''' <remarks></remarks>
    Public Overridable Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As New Hashtable
        Dim item = reqh("item") 'set defaults from request params
        Dim id = Utils.f2int(form_id) 'primary key is integer by default

        If isGet() Then 'read from db
            If id > 0 Then
                item = model0.one(id)
                'item("ftime_str") = FormUtils.int2timestr(item("ftime")) 'TODO - refactor this
            Else
                'override any defaults here
                If Me.form_new_defaults IsNot Nothing Then
                    Utils.mergeHash(item, Me.form_new_defaults)
                End If
            End If
        Else
            'read from db
            Dim itemdb = model0.one(id)
            'and merge new values from the form
            Utils.mergeHash(itemdb, item)
            item = itemdb
            'here make additional changes if necessary
        End If

        setAddUpdUser(ps, item)

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id
        If fw.FERR.Count > 0 Then logger(fw.FERR)

        Return ps
    End Function

    Public Overridable Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        'checkXSS() 'no need to check in standard SaveAction, but add to your custom actions that modifies data
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in Controller.save_fields")

        If reqi("refresh") = 1 Then
            fw.routeRedirect("ShowForm", {form_id})
            Return Nothing
        End If

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
            If Me.save_fields_nullable > "" Then FormUtils.filterNullable(itemdb, save_fields_nullable)

            id = Me.modelAddOrUpdate(id, itemdb)
        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.afterSave(success, id, is_new)
    End Function

    Public Overridable Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validateRequired(item, Me.required_fields)

        'If result AndAlso model0.isExists(item("iname"), id) Then
        '    fw.FERR("iname") = "EXISTS"
        'End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        Me.validateCheckResult()
    End Sub

    Public Overridable Sub ShowDeleteAction(ByVal form_id As String)
        Dim id As Integer = Utils.f2int(form_id)

        Dim ps = New Hashtable From {
            {"i", model0.one(id)},
            {"related_id", Me.related_id},
            {"return_url", Me.return_url},
            {"base_url", Me.base_url}
        }

        fw.parser("/common/form/showdelete", ps)
    End Sub

    Public Overridable Function DeleteAction(ByVal form_id As String) As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        model0.delete(id)
        fw.FLASH("onedelete", 1)
        Return Me.afterSave(True)
    End Function

    Public Overridable Function SaveMultiAction() As Hashtable
        Dim cbses As Hashtable = reqh("cb")
        Dim is_delete As Boolean = fw.FORM.ContainsKey("delete")
        Dim user_lists_id As Integer = reqi("addtolist")
        Dim remove_user_lists_id = reqi("removefromlist")
        Dim ctr As Integer = 0

        If user_lists_id > 0 Then
            Dim user_lists = fw.model(Of UserLists).one(user_lists_id)
            If user_lists.Count = 0 OrElse user_lists("add_users_id") <> fw.model(Of Users).meId Then Throw New ApplicationException("Wrong Request")
        End If

        For Each id As String In cbses.Keys
            If is_delete Then
                model0.delete(id)
                ctr += 1
            ElseIf user_lists_id > 0 Then
                fw.model(Of UserLists).addItemList(user_lists_id, id)
                ctr += 1
            ElseIf remove_user_lists_id > 0 Then
                fw.model(Of UserLists).delItemList(remove_user_lists_id, id)
                ctr += 1
            End If
        Next

        If is_delete Then fw.FLASH("multidelete", ctr)
        If user_lists_id > 0 Then fw.FLASH("success", ctr & " records added to the list")

        Return Me.afterSave(True, New Hashtable From {{"ctr", ctr}})
    End Function

End Class
