' LookupManager Tables Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2017 Oleg Savchuk www.osalabs.com

Public Class AdminLookupManagerTablesController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 100

    Protected model As LookupManagerTables

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of LookupManagerTables)()
        model = model0

        base_url = "/Admin/LookupManagerTables"
        required_fields = "tname iname"
        save_fields = "tname iname idesc header_text footer_text column_id columns column_names column_types column_groups groups status"
        save_fields_checkboxes = "is_one_form is_custom_form"

        search_fields = "tname iname"
        list_sortdef = "iname asc"
        list_sortmap = Utils.qh("id|id iname|iname tname|tname")
    End Sub

    Public Overrides Function ShowFormAction(Optional form_id As String = "") As Hashtable
        Dim ps As New Hashtable
        Dim item As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        If isGet() Then 'read from db
            If id > 0 Then
                item = model0.one(id)
                'convert comma separated to newline separated
                item("list_columns") = Utils.commastr2nlstr(item("list_columns"))
                item("columns") = Utils.commastr2nlstr(item("columns"))
                item("column_names") = Utils.commastr2nlstr(item("column_names"))
                item("column_types") = Utils.commastr2nlstr(item("column_types"))
                item("column_groups") = Utils.commastr2nlstr(item("column_groups"))
            Else
                'set defaults here
                item = New Hashtable
                If Me.form_new_defaults IsNot Nothing Then
                    Utils.mergeHash(item, Me.form_new_defaults)
                End If
            End If
        Else
            'read from db
            item = model0.one(id)
            'convert comma separated to newline separated
            item("list_columns") = Utils.commastr2nlstr(item("list_columns"))
            item("columns") = Utils.commastr2nlstr(item("columns"))
            item("column_names") = Utils.commastr2nlstr(item("column_names"))
            item("column_types") = Utils.commastr2nlstr(item("column_types"))
            item("column_groups") = Utils.commastr2nlstr(item("column_groups"))

            'and merge new values from the form
            Utils.mergeHash(item, reqh("item"))
            'here make additional changes if necessary
        End If

        ps("add_users_id_name") = fw.model(Of Users).iname(item("add_users_id"))
        ps("upd_users_id_name") = fw.model(Of Users).iname(item("upd_users_id"))

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id

        Return ps
    End Function

    Public Overrides Function SaveAction(Optional form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

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

            'convert from newline to comma str
            itemdb("list_columns") = Utils.nlstr2commastr(itemdb("list_columns"))
            itemdb("columns") = Utils.nlstr2commastr(itemdb("columns"))
            itemdb("column_names") = Utils.nlstr2commastr(itemdb("column_names"))
            itemdb("column_types") = Utils.nlstr2commastr(itemdb("column_types"))
            itemdb("column_groups") = Utils.nlstr2commastr(itemdb("column_groups"))

            id = Me.modelAddOrUpdate(id, itemdb)

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.afterSave(success, id, is_new)
    End Function
End Class
