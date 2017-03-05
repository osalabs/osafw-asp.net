' Events model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class Events
    Inherits FwModel
    Public log_table_name As String = "event_log"

    Public Sub New()
        MyBase.New()
        table_name = "events"
    End Sub

    'just return first row by icode field (you may want to make it unique)
    Public Function one_by_icode(icode As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where("icode") = icode
        Return db.row(table_name, where)
    End Function

    Public Overloads Sub log_event(ev_icode As String, Optional item_id As Integer = 0, Optional item_id2 As Integer = 0, Optional iname As String = "", Optional records_affected As Integer = 0)
        Dim hEV As Hashtable = one_by_icode(ev_icode)
        If Not hEV.ContainsKey("id") Then
            fw.logger(LogLevel.WARN, "No event defined for icode=[", ev_icode, "], auto-creating")
            hEV = New Hashtable
            hEV("icode") = ev_icode
            hEV("iname") = ev_icode
            hEV("idesc") = "auto-created"
            hEV("id") = Me.add(hEV)
        End If

        Dim fields As New Hashtable
        fields("events_id") = hEV("id")
        fields("item_id") = item_id
        fields("item_id2") = item_id2
        fields("iname") = iname
        fields("records_affected") = records_affected
        fields("add_user_id") = fw.model(Of Users).me_id()
        db.insert(log_table_name, fields)
    End Sub

    'just for short form call
    'Public Overloads Sub log_event(ev_icode As String, item_id As Integer)
    '    log_event(ev_icode, item_id, 0, "", 0)
    'End Sub


End Class
