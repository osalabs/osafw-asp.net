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

    Public Overrides Function ShowAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As Hashtable = MyBase.ShowAction(form_id)

        ps("fields") = prepareShowFields(ps("i"), ps)

        Return ps
    End Function

    Public Overrides Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'Me.form_new_defaults = New Hashtable 'set new form defaults here if any
        'Me.form_new_defaults = reqh("item") 'OR optionally set defaults from request params
        'item("field")="default value"
        Dim ps As Hashtable = MyBase.ShowFormAction(form_id)

        'read dropdowns lists from db
        Dim item As Hashtable = ps("i")

        ps("fields") = prepareShowFormFields(item, ps)

        'ps("select_options_parent_id") = model.listSelectOptionsParent()
        'FormUtils.comboForDate(item("fdate_combo"), ps, "fdate_combo")

        Return ps
    End Function

    Public Overrides Function modelAddOrUpdate(id As Integer, fields As Hashtable) As Integer
        Dim item As Hashtable = reqh("item")

        'TODO implement auto-processing based on me.config("showform_fields")
        fields("dict_link_auto_id") = model_related.findOrAddByIname(item("dict_link_auto_id"))
        fields("dict_link_multi") = FormUtils.multi2ids(reqh("dict_link_multi" & "_multi"))
        'fields("fdate_combo") = FormUtils.dateForCombo(item, "fdate_combo")
        'fields("ftime") = FormUtils.timeStrToInt(item("ftime_str")) 'ftime - convert from HH:MM to int (0-24h in seconds)
        fields("fint") = Utils.f2int(fields("fint")) 'field accepts only int

        id = MyBase.modelAddOrUpdate(id, fields)

        fw.model(Of Att).updateAttLinks(model.table_name, id, reqh("att"))
        Return id
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
