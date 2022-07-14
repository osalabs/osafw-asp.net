' Reports Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminReportsController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 80
    Public Shared Shadows route_default_action As String = "show"
    Protected model As New Reports

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        required_fields = "iname" 'default required fields, space-separated
        base_url = "/Admin/Reports" 'base url for the controller
    End Sub

    Public Function IndexAction() As Hashtable
        Dim ps As Hashtable = New Hashtable

        Return ps
    End Function

    Public Sub ShowAction(repcode As String)
        Dim ps As Hashtable = New Hashtable
        repcode = model.cleanupRepcode(repcode)

        ps("is_run") = reqs("dofilter") > "" OrElse reqs("is_run") > ""

        'report filters (options)
        Dim f As Hashtable = initFilter("AdminReports." & repcode)

        'get format directly form request as we don't need to remember format 
        f("format") = reqh("f")("format")
        If Not f("format") > "" Then f("format") = "html"

        Dim report = model.createInstance(repcode, f)

        ps("filter") = report.getReportFilters() 'filter data like select/lookups
        ps("f") = report.f 'filter values

        If ps("is_run") Then
            ps("rep") = report.getReportData()
        End If

        'show or output report according format
        report.render(ps)
    End Sub

    'save changes from editable reports
    Public Sub SaveAction()
        Dim repcode = model.cleanupRepcode(reqs("repcode"))

        Dim report = model.createInstance(repcode, reqh("f"))

        Try
            If report.saveChanges() Then
                fw.redirect(base_url & "/" & repcode & "?is_run=1")
            Else
                fw.FORM("is_run") = 1
                Dim args() As [String] = {repcode}
                fw.routeRedirect("Show", Nothing, args)
            End If
        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            Dim args() As [String] = {repcode}
            fw.routeRedirect("Show", Nothing, args)
        End Try
    End Sub

End Class
