' User Views Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2019 Oleg Savchuk www.osalabs.com

Public Class MyViewsController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 0

    Protected model As UserViews

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of UserViews)()
        model = model0

        'initialization
        base_url = "/My/Views"
        required_fields = "iname"
        save_fields = "screen iname status"
        save_fields_checkboxes = "is_system"

        search_fields = "iname"
        list_sortdef = "iname asc"   'default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time entity|screen")

        related_id = reqs("related_id")
    End Sub

    Public Overrides Function initFilter(Optional session_key As String = Nothing) As Hashtable
        Dim result = MyBase.initFilter(session_key)
        If Not Me.list_filter.ContainsKey("screen") Then
            Me.list_filter("screen") = related_id
        End If
        Return Me.list_filter
    End Function

    Public Overrides Sub setListSearch()
        list_where = " status<>127 and add_users_id = " & db.qi(fw.model(Of Users).meId) 'only logged user lists

        MyBase.setListSearch()

        If list_filter("screen") > "" Then
            Me.list_where &= " and screen=" & db.q(list_filter("screen"))
        End If
    End Sub

    Public Overrides Function ShowFormAction(Optional form_id As String = "") As Hashtable
        Me.form_new_defaults = New Hashtable
        Me.form_new_defaults("screen") = related_id
        Dim ps = MyBase.ShowFormAction(form_id)
        ps("is_admin") = fw.SESSION("access_level") = Users.ACL_ADMIN
        Return ps
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

            If is_new Then
                'read new filter data from session
                itemdb("idesc") = Utils.jsonEncode(fw.SESSION("_filter_" & item("screen")))
            End If

            id = Me.modelAddOrUpdate(id, itemdb)

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        If return_url > "" Then
            fw.redirect(return_url)
        End If

        Return Me.afterSave(success, id, is_new)
    End Function

End Class
