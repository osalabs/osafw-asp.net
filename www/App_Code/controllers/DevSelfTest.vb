' Self Test controller - only available for Site Admins
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com
Imports System.IO

Public Class DevSelfTestController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Protected Test As FwSelfTest

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'initialization
        base_url = "/Dev/SelfTest"
        Test = New FwSelfTest(fw)
        'Test.exclude_controllers = "AdminDemos AdminDemoDicts AdminDemoDynamic"
    End Sub

    Public Sub IndexAction()
        Test.echo_start()
        Test.all()
        'either inherit FwSelfTest and override all/some test
        'or add here tests specific for the site
        Test.echo_totals()
    End Sub

    'just have this stub here, so we don't call IndexAction and stuck in a recursion 
    Public Function SelfTest(t As FwSelfTest) As FwSelfTest.Result
        Return FwSelfTest.Result.OK
    End Function


End Class
