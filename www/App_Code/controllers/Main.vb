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
        'one("rows") = fw.model(Of Demos).getCount()
        panes("logins") = one

        one = New Hashtable
        one("type") = "piechart"
        one("title") = "User by Type"
        one("id") = "user_types"
        'one("url") = "/Admin/Reports/sample"
        'one("rows") = fw.model(Of Demos).getCount()
        panes("usertypes") = one

        Return ps
    End Function

End Class

