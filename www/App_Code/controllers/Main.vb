' Main Page for Logged user controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class MainController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 0

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
        panes("pages") = one

        one = New Hashtable
        one("type") = "bignum"
        one("title") = "Uploads"
        one("url") = "/Admin/Att"
        one("value") = fw.model(Of Att).getCount()
        panes("uploads") = one

        one = New Hashtable
        one("type") = "bignum"
        one("title") = "Users"
        one("url") = "/Admin/Users"
        one("value") = fw.model(Of Users).getCount()
        panes("users") = one

        one = New Hashtable
        one("type") = "bignum"
        one("title") = "Demo items"
        one("url") = "/Admin/DemosDynamic"
        one("value") = fw.model(Of Demos).getCount()
        panes("demos") = one

        one = New Hashtable
        one("type") = "barchart"
        one("title") = "Logins per day"
        one("id") = "logins_per_day"
        'one("url") = "/Admin/Reports/sample"
        one("rows") = db.array("with zzz as (" _
            & " select TOP 14 CAST(el.add_time as date) as idate, count(*) as ivalue from events ev, event_log el where ev.icode='login' and el.events_id=ev.id" _
            & " group by CAST(el.add_time as date) order by CAST(el.add_time as date) desc)" _
            & " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate")
        logger(one("rows"))
        panes("logins") = one

        one = New Hashtable
        one("type") = "piechart"
        one("title") = "Users by Type"
        one("id") = "user_types"
        'one("url") = "/Admin/Reports/sample"
        one("rows") = db.array("select access_level, count(*) as ivalue from users group by access_level order by count(*) desc")
        For Each row As Hashtable In one("rows")
            row("ilabel") = FormUtils.selectTplName("/common/sel/access_level.sel", row("access_level"))
        Next
        panes("usertypes") = one

        Return ps
    End Function

End Class

