' Sitemap controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports System.Net
Imports System.IO

Public Class SitemapController
    Inherits FwController
    Protected model As Spages

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of Spages)()
        model = model0

        base_url = "/sitemap"
        'override layout
        fw.G("PAGE_LAYOUT") = fw.G("PAGE_LAYOUT_PUBLIC")
    End Sub

    Public Function IndexAction() As Hashtable
        Dim ps As Hashtable = New Hashtable

        Dim item As Hashtable = model.oneByFullUrl(base_url)

        Dim pages_tree As ArrayList = model.tree(" status=0 ", "parent_id, prio desc, iname")
        _add_full_url(pages_tree)

        ps("page") = item
        ps("pages_tree") = pages_tree
        ps("hide_sidebar") = True 'TODO - control via item[template]
        Return ps
    End Function

    Private Sub _add_full_url(pages_tree As ArrayList, Optional parent_url As String = "")
        If IsNothing(pages_tree) Then Exit Sub

        For Each row As Hashtable In pages_tree
            row("full_url") = parent_url & "/" & row("url")
            _add_full_url(row("children"), row("url"))
        Next
    End Sub
End Class

