' Base API controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2017 Oleg Savchuk www.osalabs.com

Public Class FwApiController
    Inherits FwController
    'Public Shared Shadows route_default_action As String = "index" 'empty|index|show - calls IndexAction or ShowAction accordingly if no requested controller action found. If empty (default) - show template from /cur_controller/cur_action dir

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'DEFINE in inherited controllers like this:
        'base_url = "/Admin/Base"
        'required_fields = "iname"
        'save_fields = "iname idesc status"
        'save_fields_checkboxes = ""

        'search_fields = "iname idesc"
        'list_sortdef = "iname asc"
        'list_sortmap = Utils.qh("id|id iname|iname add_time|add_time")

        'list_view = model0.table_name 'optionally override list view/table
    End Sub

    Protected Overridable Function auth() As Boolean
        Dim result = False

        If fw.SESSION("is_logged") Then result = True
        If Not result Then Throw New ApplicationException("API auth error")

        Return result
    End Function

    'send output seaders
    'and if auth requested - check authorization
    Protected Overridable Sub prepare(Optional isAuth As Boolean = True)
        'logger(fw.req.Headers)

        Dim origin = ""
        If Not IsNothing(fw.req.Headers("Origin")) Then
            origin = fw.req.Headers("Origin")
        Else
            'try referrer
            If Not IsNothing(fw.req.UrlReferrer) Then
                origin = fw.req.UrlReferrer.GetLeftPart(UriPartial.Authority)
            End If
        End If
        ' logger(fw.config("hostname"))
        ' logger("referer:")
        ' logger(referrer)

        'validate referrer is same as our hostname
        If String.IsNullOrEmpty(origin) OrElse (origin <> "http://" & fw.config("hostname") AndAlso origin <> "https://" & fw.config("hostname") AndAlso origin <> fw.config("API_ALLOW_ORIGIN")) Then Throw New ApplicationException("Invalid origin " & origin)

        'create headers
        fw.resp.Headers.Remove("Access-Control-Allow-Origin")
        fw.resp.AddHeader("Access-Control-Allow-Origin", origin)

        fw.resp.Headers.Remove("Access-Control-Allow-Credentials")
        fw.resp.AddHeader("Access-Control-Allow-Credentials", "true")

        fw.resp.Headers.Remove("Access-Control-Allow-Methods")
        fw.resp.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")

        'check auth
        If isAuth Then Me.auth()
    End Sub

End Class
