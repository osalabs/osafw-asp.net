' Demo Dynamic Admin controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2020 Oleg Savchuk www.osalabs.com

Public Class AdminDemosDynamicController
    Inherits FwDynamicController
    Public Shared Shadows access_level As Integer = 80

    Protected model As Demos

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        'use if config doesn't contains model name
        'model0 = fw.model(Of Demos)()
        'model = model0

        base_url = "/Admin/DemosDynamic"
        Me.loadControllerConfig()
        model = model0
        db = model.getDB() 'model-based controller works with model's db

        model_related = fw.model(Of DemoDicts)()
        is_userlists = True

        'override sortmap for date fields
        list_sortmap("fdate_pop_str") = "fdate_pop"
    End Sub

End Class
