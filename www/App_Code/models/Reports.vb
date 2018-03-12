' Reports model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class Reports
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        'table_name = "demo_dicts"
    End Sub

    Function cleanupRepcode(repcode As String) As String
        Return LCase(Regex.Replace(repcode, "[^\w-]", ""))
    End Function
    ''' <summary>
    ''' Convert report code into class name
    ''' </summary>
    ''' <param name="repcode">pax-something-summary</param>
    ''' <returns>ReportPaxSomethingSummary</returns>
    ''' <remarks></remarks>
    Function repcodeToClass(repcode As String) As String
        Dim result As String = ""
        Dim pieces As String() = Split(repcode, "-")
        For Each piece As String In pieces
            result &= Utils.capitalize(piece)
        Next
        Return "Report" & result
    End Function

    ''' <summary>
    ''' Create instance of report class by repcode
    ''' </summary>
    ''' <param name="repcode">cleaned report code</param>
    ''' <param name="f">filters passed from request</param>
    ''' <returns></returns>
    Function createInstance(repcode As String, f As Hashtable) As ReportBase
        Dim report_class_name As String = repcodeToClass(repcode)
        If Not report_class_name > "" Then Throw New ApplicationException("Wrong Report Code")

        Dim report As ReportBase = Activator.CreateInstance(Type.GetType(report_class_name, True))
        report.init(fw, repcode, f)
        Return report
    End Function

End Class
