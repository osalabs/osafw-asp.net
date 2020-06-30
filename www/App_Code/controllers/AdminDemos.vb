' Demo Admin controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2017 Oleg Savchuk www.osalabs.com

Public Class AdminDemosController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 80

    Protected model As Demos
    Protected model_related As DemoDicts

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of Demos)()
        model = model0

        base_url = "/Admin/Demos"
        required_fields = "iname"
        save_fields = "parent_id demo_dicts_id iname idesc email fint ffloat fcombo fradio fyesno fdate_pop fdatetime att_id status"
        save_fields_checkboxes = "is_checkbox|0"

        search_fields = "iname idesc"
        list_sortdef = "iname asc"
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time demo_dicts_id|demo_dicts_id email|email status|status")

        related_field_name = "demo_dicts_id"
        model_related = fw.model(Of DemoDicts)()
    End Sub

    Public Overrides Sub getListRows()
        MyBase.getListRows()

        'add/modify rows from db if necessary
        For Each row As Hashtable In Me.list_rows
            row("demo_dicts") = model_related.one(Utils.f2int(row("demo_dicts_id")))
        Next
    End Sub

    Public Overrides Function ShowAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As Hashtable = MyBase.ShowAction(form_id)
        Dim item As Hashtable = ps("i")
        Dim id = Utils.f2int(item("id"))

        ps("parent") = model.one(Utils.f2int(item("parent_id")))
        ps("demo_dicts") = model_related.one(Utils.f2int(item("demo_dicts_id")))
        ps("dict_link_auto") = model_related.one(Utils.f2int(item("dict_link_auto_id")))
        ps("multi_datarow") = model_related.getMultiList(item("dict_link_multi"))
        ps("multi_datarow_link") = model_related.getMultiListAL(model.getLinkedIds(model.table_link, id, "demos_id", "demo_dicts_id"))
        ps("att") = fw.model(Of Att).one(Utils.f2int(item("att_id")))
        ps("att_links") = fw.model(Of Att).getAllLinked(model.table_name, id)

        Return ps
    End Function

    Public Overrides Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'Me.form_new_defaults = New Hashtable 'set new form defaults here if any
        'Me.form_new_defaults = reqh("item") 'OR optionally set defaults from request params
        'item("field")="default value"
        Dim ps As Hashtable = MyBase.ShowFormAction(form_id)

        'read dropdowns lists from db
        Dim item As Hashtable = ps("i")
        Dim id = Utils.f2int(item("id"))
        ps("select_options_parent_id") = model.listSelectOptionsParent()
        ps("select_options_demo_dicts_id") = model_related.listSelectOptions()
        ps("dict_link_auto_id_iname") = model_related.iname(item("dict_link_auto_id"))
        ps("multi_datarow") = model_related.getMultiList(item("dict_link_multi"))
        ps("multi_datarow_link") = model_related.getMultiListAL(model.getLinkedIds(model.table_link, id, "demos_id", "demo_dicts_id"))
        FormUtils.comboForDate(item("fdate_combo"), ps, "fdate_combo")

        ps("att") = fw.model(Of Att).one(Utils.f2int(item("att_id")))
        ps("att_links") = fw.model(Of Att).getAllLinked(model.table_name, id)

        Return ps
    End Function

    Public Overrides Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

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
            'Dim item_old As Hashtable = model.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Me.save_fields)
            FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes)
            itemdb("dict_link_auto_id") = model_related.findOrAddByIname(item("dict_link_auto_id_iname"))
            itemdb("dict_link_multi") = FormUtils.multi2ids(reqh("dict_link_multi"))
            itemdb("fdate_combo") = FormUtils.dateForCombo(item, "fdate_combo")
            itemdb("ftime") = FormUtils.timeStrToInt(item("ftime_str")) 'ftime - convert from HH:MM to int (0-24h in seconds)
            itemdb("fint") = Utils.f2int(itemdb("fint")) 'field accepts only int

            id = Me.modelAddOrUpdate(id, itemdb)

            model.updateLinked(model.table_link, id, "demos_id", "demo_dicts_id", reqh("demo_dicts_link"))
            fw.model(Of Att).updateAttLinks(model.table_name, id, reqh("att"))

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.afterSave(success, id, is_new)
    End Function

    Public Overrides Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validateRequired(item, Me.required_fields)

        If result AndAlso model.isExists(item("email"), id) Then
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.isEmail(item("email")) Then
            fw.FERR("email") = "WRONG"
        End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        Me.validateCheckResult()
    End Sub

    Public Function AutocompleteAction() As Hashtable
        Dim items As ArrayList = model_related.getAutocompleteList(reqs("q"))

        Return New Hashtable From {{"_json", items}}
    End Function

End Class
