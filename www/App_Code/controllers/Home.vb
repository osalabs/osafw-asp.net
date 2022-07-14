' Home Page controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class HomeController
    Inherits FwController
    Public Shared Shadows route_default_action As String = "show"

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        'override global layout because for this controller we need public pages template, not admin pages
        fw.G("PAGE_LAYOUT") = fw.config("PAGE_LAYOUT_PUBLIC")
    End Sub

    'CACHED as home_page
    Public Function IndexAction() As Hashtable
        Dim ps As Hashtable = FwCache.getValue("home_page")

        If IsNothing(ps) OrElse ps.Count = 0 Then
            'CACHE MISS
            ps = New Hashtable

            'create home page with heavy queries

            FwCache.setValue("home_page", ps)
        End If
        ps("hide_sidebar") = True
        'ps("_layout") = fw.config("PAGE_LAYOUT_PUBLIC") 'alternatively override layout just for this action
        Return ps
    End Function

    Public Sub ShowAction(Optional ByVal id As String = "")
        Dim ps As Hashtable = New Hashtable
        ps("hide_sidebar") = True 'TODO control via pages
        fw.parser("/home/" & Utils.routeFixChars(LCase(id)), fw.config("PAGE_LAYOUT_PUBLIC"), ps)
    End Sub

    'called if fw.dispatch can't find controller
    Public Sub NotFoundAction()
        fw.model(Of Spages).showPageByFullUrl(fw.request_url)
    End Sub

    Public Sub TestAction(Optional ByVal id As String = "")
        Dim hf As Hashtable = New Hashtable
        logger("in the TestAction")
        rw("here it is Test")
        rw("id=" & id)
        rw("more_action_name=" & fw.route.action_more)

        'parser("/index", hf)
    End Sub
    
End Class

