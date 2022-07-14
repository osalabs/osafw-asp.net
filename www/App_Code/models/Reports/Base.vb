' Reports Base class
'
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class ReportBase
    Protected fw As FW
    Protected db As DB
    Public report_code As String
    Public format As String 'report format, if empty - html, other options: html, csv, pdf, xls
    Public f As Hashtable 'report filters/options
    'render options for html to pdf/xls/etc... convertor
    Public render_options As New Hashtable From {
            {"cmd", "--page-size Letter --print-media-type"},
            {"landscape", True}
        }

    Public Sub New()
    End Sub

    Public Overridable Sub init(fw As FW, report_code As String, f As Hashtable)
        Me.fw = fw
        Me.db = fw.db
        Me.report_code = report_code
        Me.f = f
        Me.format = f("format")
    End Sub

    Public Overridable Function getReportData() As Hashtable
        Dim ps As New Hashtable
        Return ps
    End Function

    Public Overridable Function getReportFilters() As Hashtable
        Dim result As New Hashtable
        'result("select_something")=fw.model(of Something).listSelectOptions()
        Return result
    End Function

    Public Overridable Function saveChanges() As Boolean
        Return False
    End Function

    'render report according to format
    Public Overridable Sub render(ps As Hashtable)
        Dim base_dir As String = "/admin/reports/" & Me.report_code
        Select Case Me.format
            Case "pdf"
                ps("f")("edit") = False 'force any edit modes off
                ps("IS_EXPORT_PDF") = True
                fw.G("IS_EXPORT_PDF") = True 'TODO make TOP[] in ParsePage?
                ConvUtils.parsePagePdf(fw, base_dir, fw.config("PAGE_LAYOUT_PRINT"), ps, report_code, render_options)
            Case "xls"
                ps("IS_EXPORT_XLS") = True
                fw.G("IS_EXPORT_XLS") = True 'TODO make TOP[] in ParsePage?
                ConvUtils.parsePageExcelSimple(fw, base_dir, "/admin/reports/common/xls.html", ps, report_code)

            Case "csv"
                Throw New NotImplementedException("CSV format not yet supported")

            Case Else
                'html
                'show report using templates from related report dir
                fw.parser(base_dir, ps)
        End Select

    End Sub

    ' REPORT HELPERS

    'add "perc" value for each row (percentage of row's "ctr" from sum of all ctr)
    Protected Function _calcPerc(rows As ArrayList) As Integer
        Dim total_ctr As Integer = 0
        For Each row As Hashtable In rows
            total_ctr += Utils.f2int(row("ctr"))
        Next
        If total_ctr > 0 Then
            For Each row As Hashtable In rows
                row("perc") = row("ctr") / total_ctr * 100
            Next
        End If
        Return total_ctr
    End Function

End Class
