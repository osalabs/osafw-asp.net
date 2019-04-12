' Demo Dynamic Admin controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

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

        model_related = fw.model(Of DemoDicts)()
    End Sub

End Class
