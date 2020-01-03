' HttpModule and HttpHandler for processing arbitrary urls with asp.net Session support
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO

' see here what handlers called in what order
' http://msdn.microsoft.com/en-us/library/system.web.httpapplication(v=vs.80).aspx

Public Class FwHttpModule
    Implements IHttpModule

    Public Sub Init(application As HttpApplication) Implements IHttpModule.Init
        AddHandler application.BeginRequest, AddressOf Application_BeginRequest
        AddHandler application.PostAcquireRequestState, AddressOf Application_PostAcquireRequestState
        AddHandler application.PostResolveRequestCache, AddressOf Application_PostResolveRequestCache
    End Sub

    'for OPTIONS preflight request
    'also requires in web.config: runAllManagedModulesForAllRequests="true"
    Private Sub Application_BeginRequest(source As Object, e As EventArgs)
        Dim app As HttpApplication = DirectCast(source, HttpApplication)
        Dim context As HttpContext = app.Context
        Dim response = context.Response

        'preflight request
        If context.Request.HttpMethod.ToUpper() = "OPTIONS" Then
            ' clear any response
            response.ClearHeaders()
            response.ClearContent()
            response.Clear()

            ' Set allowed method And headers
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")

            'allow any custom headers
            Dim requestHeaders = context.Request.Headers("Access-Control-Request-Headers")
            If Not IsNothing(requestHeaders) Then response.AppendHeader("Access-Control-Allow-Headers", requestHeaders)
            'response.AppendHeader("Access-Control-Allow-Headers", "*")
            'response.AppendHeader("Access-Control-Expose-Headers", "*")

            'allow credentials
            response.AddHeader("Access-Control-Allow-Credentials", "true")

            ' Set allowed origin
            Dim origin = context.Request.Headers("Origin")
            If Not IsNothing(origin) Then
                response.AppendHeader("Access-Control-Allow-Origin", origin)
            Else
                response.AppendHeader("Access-Control-Allow-Origin", "*")
            End If

            ' end request
            context.ApplicationInstance.CompleteRequest()
        End If

    End Sub

    Private Sub Application_PostResolveRequestCache(source As Object, e As EventArgs)
        ' check if we need to open session 
        ' for static files there are no need to open session
        Dim app As HttpApplication = DirectCast(source, HttpApplication)

        Dim context As HttpContext = app.Context
        Dim req As HttpRequest = context.Request
        Dim path As String = req.PhysicalPath

        Dim url As String = req.Path
        If req.ApplicationPath > "/" Then url = Replace(url, req.ApplicationPath, "")

        'if url is static dir - serve it as is
        If Regex.Match(url, "^/(?:css|img|scripts)").Success Then Return

        'or if file exists - serve it as is
        'if url="/" then we shouldn't check for directory existence as it's a Home controller and directory always exists
        If File.Exists(path) OrElse url <> "/" AndAlso Directory.Exists(path) Then Return
        'Or Directory.Exists(path) - not always the case

        ' no need to replace the current handler if hanler already implements session interface
        If TypeOf app.Context.Handler Is IReadOnlySessionState OrElse TypeOf app.Context.Handler Is IRequiresSessionState Then Return

        ' swap the current handler
        If HttpRuntime.UsingIntegratedPipeline Then
            'IIS 7 and 8, .net >= 2.0 SP2
            app.Context.RemapHandler(New FwHttpHandler(app.Context.Handler))
        Else
            'IIS 6, .net 2
            app.Context.Handler = New FwHttpHandler(app.Context.Handler)
        End If
    End Sub

    Private Sub Application_PostAcquireRequestState(source As Object, e As EventArgs)
        'Dim app As HttpApplication = DirectCast(source, HttpApplication)
        Dim context As HttpContext = HttpContext.Current

        Dim resourceHttpHandler As FwHttpHandler = TryCast(context.Handler, FwHttpHandler)

        'don't restore original handler if we plan to run our own
        If resourceHttpHandler IsNot Nothing AndAlso Not (TypeOf (resourceHttpHandler) Is FwHttpHandler) Then
            ' set the original handler back
            context.Handler = resourceHttpHandler.OriginalHandler
        End If

        ' at this point session state should be available
        'FW.logger(app.Session)
    End Sub

    Public Sub Dispose() Implements IHttpModule.Dispose
        'just need to be declared
    End Sub

    ' dummy handler used to force the SessionStateModule to load session state
    Public Class FwHttpHandler
        Implements IHttpHandler
        Implements IRequiresSessionState

        Friend ReadOnly OriginalHandler As IHttpHandler

        Public Sub New(Handler As IHttpHandler)
            OriginalHandler = Handler
        End Sub

        Public ReadOnly Property IsReusable() As Boolean Implements IHttpHandler.IsReusable
            Get
                ' IsReusable must be set to false since class has a member!
                'Throw New NotImplementedException()
                Return False
            End Get
        End Property

        Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
            FW.run(context)
        End Sub

    End Class

End Class