' Main Page for Logged user controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2019 Oleg Savchuk www.osalabs.com

Public Class MainController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 0

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        base_url = "/Main"
    End Sub

    Public Function IndexAction() As Hashtable
        Dim ps As Hashtable = New Hashtable

        Dim one As Hashtable
        Dim panes As New Hashtable
        ps("panes") = panes

        one = New Hashtable
        one("type") = "bignum"
        one("title") = "Pages"
        one("url") = "/Admin/Spages"
        one("value") = fw.model(Of Spages).getCount()
        panes("plate1") = one

        one = New Hashtable
        one("type") = "bignum"
        one("title") = "Uploads"
        one("url") = "/Admin/Att"
        one("value") = fw.model(Of Att).getCount()
        panes("plate2") = one

        one = New Hashtable
        one("type") = "bignum"
        one("title") = "Users"
        one("url") = "/Admin/Users"
        one("value") = fw.model(Of Users).getCount()
        panes("plate3") = one

        one = New Hashtable
        one("type") = "bignum"
        one("title") = "Demo items"
        one("url") = "/Admin/DemosDynamic"
        one("value") = fw.model(Of Demos).getCount()
        panes("plate4") = one

        one = New Hashtable
        one("type") = "barchart"
        one("title") = "Logins per day"
        one("id") = "logins_per_day"
        'one("url") = "/Admin/Reports/sample"
        one("rows") = db.array("with zzz as (" _
            & " select TOP 14 CAST(el.add_time as date) as idate, count(*) as ivalue from events ev, event_log el where ev.icode='login' and el.events_id=ev.id" _
            & " group by CAST(el.add_time as date) order by CAST(el.add_time as date) desc)" _
            & " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate")
        panes("barchart") = one

        one = New Hashtable
        one("type") = "piechart"
        one("title") = "Users by Type"
        one("id") = "user_types"
        'one("url") = "/Admin/Reports/sample"
        one("rows") = db.array("select access_level, count(*) as ivalue from users group by access_level order by count(*) desc")
        For Each row As Hashtable In one("rows")
            row("ilabel") = FormUtils.selectTplName("/common/sel/access_level.sel", row("access_level"))
        Next
        panes("piechart") = one

        one = New Hashtable
        one("type") = "table"
        one("title") = "Last Events"
        'one("url") = "/Admin/Reports/sample"
        one("rows") = db.array("select TOP 10 el.add_time as [On], ev.iname as Event from events ev, event_log el where el.events_id=ev.id order by el.id desc")
        one("headers") = New ArrayList
        If one("rows").Count > 0 Then
            Dim fields = DirectCast(one("rows")(0), Hashtable).Keys.Cast(Of String).ToArray()
            For Each key In fields
                one("headers").Add(New Hashtable From {{"field_name", key}})
            Next
            For Each row As Hashtable In one("rows")
                Dim cols As New ArrayList
                For Each fieldname In fields
                    cols.Add(New Hashtable From {
                    {"row", row},
                    {"field_name", fieldname},
                    {"data", row(fieldname)}
                })
                Next
                row("cols") = cols
            Next
        End If
        panes("tabledata") = one


        one = New Hashtable
        one("type") = "linechart"
        one("title") = "Events per day"
        one("id") = "eventsctr"
        'one("url") = "/Admin/Reports/sample"
        one("rows") = db.array("with zzz as (" _
            & " select TOP 14 CAST(el.add_time as date) as idate, count(*) as ivalue from events ev, event_log el where el.events_id=ev.id" _
            & " group by CAST(el.add_time as date) order by CAST(el.add_time as date) desc)" _
            & " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate")
        panes("linechart") = one

        Return ps
    End Function

    Sub ThemeAction(form_id As String)
        fw.SESSION("theme", Utils.f2int(form_id))

        fw.redirect(base_url)
    End Sub

End Class

