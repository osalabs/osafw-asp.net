' Demo Dynamic Admin controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class AdminDemosDynamicController
    Inherits FwAdminController
    Protected model As Demos
    Protected model_related As DemoDicts

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of Demos)()
        model = model0

        base_url = "/Admin/DemosDynamic"
        Me.loadControllerConfig()

        model_related = fw.model(Of DemoDicts)()
    End Sub

    Public Overrides Sub getListRows()
        MyBase.getListRows()

        'add/modify rows from db if necessary
        If related_field_name > "" Then
            For Each row As Hashtable In Me.list_rows
                row("related") = model_related.one(row(related_field_name))
            Next
        End If

    End Sub

    Public Overrides Function ShowAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As Hashtable = MyBase.ShowAction(form_id)
        Dim item As Hashtable = ps("i")
        Dim id = Utils.f2int(item("id"))

        Dim fields As ArrayList = Me.config("show_fields")
        For Each def As Hashtable In fields
            Dim dtype = def("type")
            Dim field = def("field")

            If dtype = "multi" Then
                'complex field
                def("multi_datarow") = fw.model(def("lookup_model")).getMultiList(item(field))

            ElseIf dtype = "att" Then
                def("att") = fw.model(Of Att).one(Utils.f2int(item(field)))

            ElseIf dtype = "att_links" Then
                def("att_links") = fw.model(Of Att).getAllLinked(model.table_name, Utils.f2int(id))
                logger(def("att_links"))

            Else
                'single values
                'lookups
                If def.ContainsKey("lookup_table") Then 'lookup by table
                    Dim lookup_key = def("lookup_key")
                    If lookup_key = "" Then lookup_key = "id"

                    Dim lookup_field = def("lookup_field")
                    If lookup_field = "" Then lookup_field = "iname"

                    def("lookup_row") = db.row(def("lookup_table"), New Hashtable From {{lookup_key, item(field)}})
                    def("value") = def("lookup_row")(lookup_field)

                ElseIf def.ContainsKey("lookup_model") Then 'lookup by model
                    def("lookup_row") = fw.model(def("lookup_model")).one(item(field))

                    Dim lookup_field = def("lookup_field")
                    If lookup_field = "" Then lookup_field = "iname"

                    def("value") = def("lookup_row")(lookup_field)

                ElseIf def.ContainsKey("lookup_tpl") Then
                    def("value") = FormUtils.selectTplName(def("lookup_tpl"), item(field))

                Else
                    def("value") = item(field)
                End If

                'convertors
                If def.ContainsKey("conv") Then
                    If def("conv") = "time_from_seconds" Then
                        def("value") = FormUtils.intToTimeStr(Utils.f2int(def("value")))
                    End If
                End If
            End If
        Next
        ps("fields") = fields

        ps("att") = fw.model(Of Att).one(Utils.f2int(item("att_id")))
        ps("att_links") = fw.model(Of Att).getAllLinked(model.table_name, Utils.f2int(item("id")))

        Return ps
    End Function

    Public Overrides Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'Me.form_new_defaults = New Hashtable 'set new form defaults here if any
        'Me.form_new_defaults = reqh("item") 'OR optionally set defaults from request params
        'item("field")="default value"
        Dim ps As Hashtable = MyBase.ShowFormAction(form_id)

        'read dropdowns lists from db
        Dim item As Hashtable = ps("i")
        ps("select_options_parent_id") = model.listSelectOptionsParent()
        ps("select_options_demo_dicts_id") = model_related.listSelectOptions()
        ps("dict_link_auto_id_iname") = model_related.iname(item("dict_link_auto_id"))
        ps("multi_datarow") = model_related.getMultiList(item("dict_link_multi"))
        FormUtils.comboForDate(item("fdate_combo"), ps, "fdate_combo")

        ps("att") = fw.model(Of Att).one(Utils.f2int(item("att_id")))
        ps("att_links") = fw.model(Of Att).getAllLinked(model.table_name, Utils.f2int(item("id")))

        Return ps
    End Function

    Public Overrides Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

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

            fw.model(Of Att).updateAttLinks(model.table_name, id, reqh("att"))

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.saveCheckResult(success, id, is_new)
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
