' Site Settings Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminSettingsController
    Inherits FwAdminController
    Protected model As Settings

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of Settings)()
        model = model0

        base_url = "/Admin/Settings"
        required_fields = "ivalue"
        save_fields = "ivalue"
        save_fields_checkboxes = ""

        search_fields = "icode iname ivalue"
        list_sortdef = "iname asc"
        list_sortmap = Utils.qh("id|id iname|iname upd_time|upd_time")
    End Sub

    Public Overrides Function IndexAction() As Hashtable
        'get filters from the search form
        Dim f As Hashtable = Me.get_filter()

        Me.set_list_sorting()

        Me.list_where = " 1=1 "
        Me.set_list_search()
        'set here non-standard search
        If f("s") = "" Then
            'if search - no category
            Me.list_where &= " and icat=" & db.q(f("icat"))
        End If

        Me.get_list_rows()
        'add/modify rows from db if necessary
        'For Each row As Hashtable In Me.list_rows
        '    row("field") = "value"
        'Next

        Dim ps As Hashtable = New Hashtable
        ps("list_rows") = Me.list_rows
        ps("count") = Me.list_count
        ps("pager") = Me.list_pager
        ps("f") = Me.list_filter

        Return ps
    End Function

    Public Overrides Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'set new form defaults here if any
        'Me.form_new_defaults = New Hashtable
        'item("field")="default value"
        Dim ps As Hashtable = MyBase.ShowFormAction(form_id)

        Dim item As Hashtable = ps("i")
        'TODO - multi values for select, checkboxes, radio
        'ps("select_options_parent_id") = FormUtils.select_options_db(db.array("select id, iname from " & model.table_name & " where parent_id=0 and status=0 order by iname"), item("parent_id"))
        'ps("multi_datarow") = fw.model(Of DemoDicts).get_multi_list(item("dict_link_multi"))

        Return ps
    End Function

    Public Overrides Sub SaveAction(Optional ByVal form_id As String = "")
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

        Dim item As Hashtable = req("item")
        Dim id As Integer = Utils.f2int(form_id)

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim item_old As Hashtable = model.one(id)

            Dim itemdb As Hashtable = FormUtils.form2dbhash(item, Me.save_fields)
            'TODO - checkboxes
            'FormUtils.form2dbhash_checkboxes(itemdb, item, save_fields_checkboxes)
            'itemdb("dict_link_multi") = FormUtils.multi2ids(fw.FORM("dict_link_multi"))

            'only update, no add new settings
            model.update(id, itemdb)
            fw.FLASH("record_updated", 1)

            'custom code:
            'reset cache
            FwCache.remove("main_menu")

            'fw.redirect(base_url & "/" & id & "/edit")
            fw.redirect(base_url)
        Catch ex As ApplicationException
            Me.set_form_error(ex)
            fw.route_redirect("ShowForm", New String() {id})
        End Try
    End Sub

    Public Overrides Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validate_required(item, Me.required_fields)

        If id = 0 Then Throw New ApplicationException("Wrong Settings ID")

        Me.validate_check_result()
    End Sub

    Public Overrides Sub DeleteAction(ByVal form_id As String)
        Throw New ApplicationException("Site Settings cannot be deleted")
    End Sub

End Class
