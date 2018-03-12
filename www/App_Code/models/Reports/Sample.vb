' Sample report - shows Event Log
'
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class ReportSample
    Inherits ReportBase

    Public Sub New()
        MyBase.New()

        'override report render options if necessary
        render_options("landscape") = False
    End Sub

    'define report filters, filter defaults can be set here
    Public Overrides Function getReportFilters() As Hashtable
        If Not f.ContainsKey("from_date") AndAlso Not f.ContainsKey("to_date") Then
            'default filters
            'f("from_date") = DateUtils.Date2Str(Now())
            'f("to_date") = DateUtils.Date2Str(Now())
        End If
        If f("from_date") > "" OrElse f("to_date") > "" Then f("is_dates") = True

        Return f
    End Function

    Public Overrides Function getReportData() As Hashtable
        Dim ps As New Hashtable

        'apply filters from Me.f
        Dim where As String = " "
        If f("from_date") > "" Then
            where &= " and el.add_time>=" & db.qd(f("from_date"))
        End If
        If f("to_date") > "" Then
            where &= " and el.add_time<" & db.qd(DateAdd(DateInterval.Day, 1, Utils.f2date(f("to_date"))))
        End If

        'define query
        Dim sql As String
        sql = "select el.*, e.iname  as event_name, u.fname, u.lname " &
              "  from [events] e, event_log el " &
              "       LEFT OUTER JOIN users u ON (u.id=el.add_users_id)" &
              " where el.events_id=e.id" &
                where &
                " order by el.id desc"
        ps("rows") = db.array(sql)

        'perform calculations and add additional info for each result row
        'For Each row As Hashtable In ps("rows")
        '    row("event") = fw.model(Of FwEvents).one(Utils.f2int(row("events_id")))
        'Next
        'ps("total_ctr") = _calcPerc(ps("rows")) - if you need calculate "perc" for each row based on row("ctr")

        Return ps
        'hint: use <~rep[rows]> and <~f[from_date]> in /admin/reports/sample/report_html.html
    End Function

End Class
