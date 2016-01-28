' Demo Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class AdminDemosController
    Inherits FwAdminController
    Protected model As Demos

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of Demos)()
        model = model0

        base_url = "/Admin/Demos"
        required_fields = "iname"
        save_fields = "parent_id demo_dicts_id iname idesc email fint ffloat fcombo fradio fyesno fdate_pop fdatetime status"
        save_fields_checkboxes = "is_checkbox|0"

        search_fields = "iname idesc"
        list_sortdef = "iname asc"
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time demo_dicts_id|demo_dicts_id email|email")
    End Sub

    Public Overrides Function IndexAction() As Hashtable
        Dim ps As Hashtable = MyBase.IndexAction()

        'add/modify rows from db if necessary
        For Each row As Hashtable In Me.list_rows
            row("demo_dicts") = fw.model(Of DemoDicts).one(row("demo_dicts_id"))
        Next

        Return ps
    End Function

    Public Overrides Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'set new form defaults here if any
        'Me.form_new_defaults = New Hashtable
        'item("field")="default value"
        Dim ps As Hashtable = MyBase.ShowFormAction(form_id)

        'read dropdowns lists from db
        Dim item As Hashtable = ps("i")
        ps("select_options_parent_id") = FormUtils.select_options_db(db.array("select id, iname from " & model.table_name & " where parent_id=0 and status=0 order by iname"), item("parent_id"))
        ps("select_options_demo_dicts_id") = fw.model(Of DemoDicts).get_select_options(item("demo_dicts_id"))
        ps("dict_link_auto_id_iname") = fw.model(Of DemoDicts).iname(item("dict_link_auto_id"))
        ps("multi_datarow") = fw.model(Of DemoDicts).get_multi_list(item("dict_link_multi"))
        FormUtils.combo4date(item("fdate_combo"), ps, "fdate_combo")

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
            FormUtils.form2dbhash_checkboxes(itemdb, item, save_fields_checkboxes)
            itemdb("dict_link_auto_id") = fw.model(Of DemoDicts).add_or_update_quick(item("dict_link_auto_id_iname"))
            itemdb("dict_link_multi") = FormUtils.multi2ids(fw.FORM("dict_link_multi"))
            itemdb("fdate_combo") = FormUtils.date4combo(item, "fdate_combo")
            itemdb("ftime") = FormUtils.timestr2int(item("ftime_str")) 'ftime - convert from HH:MM to int (0-24h in seconds)

            id = Me.model_add_or_update(id, itemdb)

            fw.redirect(base_url & "/" & id & "/edit")
        Catch ex As ApplicationException
            Me.set_form_error(ex)
            fw.route_redirect("ShowForm", New String() {id})
        End Try
    End Sub

    Public Overrides Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validate_required(item, Me.required_fields)

        If result AndAlso model.is_exists(item("email"), id) Then
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.is_email(item("email")) Then
            fw.FERR("email") = "WRONG"
        End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        Me.validate_check_result()
    End Sub

    Public Function AjaxAutocompleteAction() As Hashtable
        Dim jout As New Hashtable
        Dim query As String = fw.FORM("q")

        Dim items As ArrayList = fw.model(Of DemoDicts)().get_autocomplete_items(query)

        Dim ps As New Hashtable
        ps("_json_enabled") = True
        ps("_json_data") = items
        Return ps
    End Function

End Class
