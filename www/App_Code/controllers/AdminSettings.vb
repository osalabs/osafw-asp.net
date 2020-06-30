' Site Settings Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminSettingsController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 100

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

    Public Overrides Sub setListSearch()
        Me.list_where = " 1=1 "
        MyBase.setListSearch()

        If list_filter("s") > "" Then list_where &= " and icat=" & db.qi(list_filter("s"))
    End Sub

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

    Public Overrides Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)
        Dim success = True
        Dim is_new = (id = 0)
        Dim location = ""

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim item_old As Hashtable = model.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Me.save_fields)
            'TODO - checkboxes
            'FormUtils.form2dbhash_checkboxes(itemdb, item, save_fields_checkboxes)
            'itemdb("dict_link_multi") = FormUtils.multi2ids(reqh("dict_link_multi"))

            'only update, no add new settings
            model.update(id, itemdb)
            fw.FLASH("record_updated", 1)

            'custom code:
            'reset cache
            FwCache.remove("main_menu")

            location = base_url
        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.afterSave(success, id, is_new, "ShowForm", location)
    End Function

    Public Overrides Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validateRequired(item, Me.required_fields)

        If id = 0 Then Throw New ApplicationException("Wrong Settings ID")

        Me.validateCheckResult()
    End Sub

    Public Overrides Function DeleteAction(ByVal form_id As String) As Hashtable
        Throw New ApplicationException("Site Settings cannot be deleted")
    End Function

End Class
