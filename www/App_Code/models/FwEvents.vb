' Fw Events model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class FwEvents
    Inherits FwModel
    Public log_table_name As String = "event_log"

    Public Sub New()
        MyBase.New()
        table_name = "events"
    End Sub

    'just return first row by icode field (you may want to make it unique)
    Public Function oneByIcode(icode As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where("icode") = icode
        Return db.row(table_name, where)
    End Function

    Public Overloads Sub log(ev_icode As String, Optional item_id As Integer = 0, Optional item_id2 As Integer = 0, Optional iname As String = "", Optional records_affected As Integer = 0, Optional changed_fields As Hashtable = Nothing)
        Dim hEV As Hashtable = oneByIcode(ev_icode)
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
        If changed_fields IsNot Nothing Then fields("fields") = Utils.jsonEncode(changed_fields)
        fields("add_users_id") = fw.model(Of Users).meId()
        db.insert(log_table_name, fields)
    End Sub

    'just for short form call
    'Public Overloads Sub logEvent(ev_icode As String, item_id As Integer)
    '    log(ev_icode, item_id, 0, "", 0)
    'End Sub

    ''' <summary>
    ''' leave in only those item keys, which are apsent/different from itemold
    ''' </summary>
    ''' <param name="item"></param>
    ''' <param name="itemold"></param>
    Public Function changes_only(item As Hashtable, itemold As Hashtable) As Hashtable
        Dim result As New Hashtable
        Dim datenew As Object
        Dim dateold As Object
        Dim vnew As Object
        Dim vold As Object
        For Each key In item.Keys
            vnew = item(key)
            vold = itemold(key)

            datenew = Utils.f2date(vnew)
            dateold = Utils.f2date(vold)
            If datenew IsNot Nothing AndAlso dateold IsNot Nothing Then
                'it's dates - only compare DATE part, not time as all form inputs are dates without times
                vnew = CType(datenew, Date).ToShortDateString()
                vold = CType(dateold, Date).ToShortDateString()
            End If

            'If Not itemold.ContainsKey(key) _
            '    OrElse vnew Is Nothing AndAlso vold IsNot Nothing _
            '    OrElse vnew IsNot Nothing AndAlso vold Is Nothing _
            '    OrElse vnew IsNot Nothing AndAlso vold IsNot Nothing _
            '        AndAlso vnew.ToString() <> vold.ToString() _
            '    Then
            If Not itemold.ContainsKey(key) _
                 OrElse Utils.f2str(vnew) <> Utils.f2str(vold) _
                Then
                'logger("****:" & key)
                'logger(TypeName(vnew) & " - " & vnew & " - " & datenew)
                'logger(TypeName(vold) & " - " & vold & " - " & dateold)
                result(key) = item(key)
            End If
        Next
        Return result
    End Function

    ''' <summary>
    ''' return true if any of passed fields changed
    ''' </summary>
    ''' <param name="item1"></param>
    ''' <param name="item2"></param>
    ''' <param name="fields">qw-list of fields</param>
    ''' <returns>false if no chagnes in passed fields or fields are empty</returns>
    Public Function is_changed(item1 As Hashtable, item2 As Hashtable, fields As String) As Boolean
        Dim result = False
        Dim afields = Utils.qw(fields)
        For Each fld In afields
            If item1.ContainsKey(fld) AndAlso item2.ContainsKey(fld) AndAlso Utils.f2str(item1(fld)) <> Utils.f2str(item2(fld)) Then
                result = True
                Exit For
            End If
        Next

        Return result
    End Function

    'check if 2 dates (without time) chagned
    Public Function is_changed_date(date1 As Object, date2 As Object) As Boolean
        Dim dt1 = Utils.f2date(date1)
        Dim dt2 = Utils.f2date(date2)

        If dt1 IsNot Nothing OrElse dt2 IsNot Nothing Then
            If dt1 IsNot Nothing AndAlso dt2 IsNot Nothing Then
                'both set - compare dates
                If DateUtils.Date2SQL(dt1) <> DateUtils.Date2SQL(dt2) Then Return True
            Else
                'one set, one no - chagned
                Return True
            End If
        Else
            'both empty - not changed
        End If

        Return False
    End Function

End Class
