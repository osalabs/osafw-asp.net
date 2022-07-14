' Framework core class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2019 Oleg Savchuk www.osalabs.com

Imports System.IO
Imports System.Net.Mail
Imports System.Reflection

'Custom Exceptions
<Serializable>
Public Class AuthException : Inherits ApplicationException
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class
<Serializable>
Public Class UserException : Inherits ApplicationException
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class
<Serializable>
Public Class ValidationException : Inherits ApplicationException
End Class
<Serializable>
Public Class RedirectException : Inherits Exception
End Class

''' <summary>
''' Logger levels, ex: logger(LogLevel.ERROR, "Something happened")
''' </summary>
Public Enum LogLevel As Integer
    OFF             'no logging occurs
    FATAL           'severe error, current request (or even whole application) aborted (notify admin)
    [ERROR]         'error happened, but current request might still continue (notify admin)
    WARN            'potentially harmful situations for further investigation, request processing continues
    INFO            'default for production (easier maintenance/support), progress of the application at coarse-grained level (fw request processing: request start/end, sql, route/external redirects, sql, fileaccess, third-party API)
    DEBUG           'default for development (default for logger("msg") call), fine-grained level
    TRACE           'very detailed dumps (in-module details like fw core, despatcher, parse page, ...)
    ALL             'just log everything
End Enum

Public Class FwRoute
    Public controller_path As String 'store /Prefix/Controller - to use in parser a default path for templates
    Public method As String
    Public controller As String
    Public action As String
    Public action_raw As String
    Public id As String
    Public action_more As String
    Public format As String
    Public params As ArrayList
End Class

'Main Framework Class
Public Class FW
    Implements IDisposable
    Public Shared METHOD_ALLOWED As Hashtable = Utils.qh("GET POST PUT DELETE")


    Private floggerFS As System.IO.FileStream
    Private floggerSW As System.IO.StreamWriter

    Private ReadOnly models As New Hashtable
    Public Shared Current As FW 'store FW current "singleton", set in run WARNING - avoid to use as if 2 parallel requests come, a bit later one will overwrite this
    Public cache As New FwCache 'request level cache

    Public FORM As Hashtable
    Public G As Hashtable 'for storing global vars - used in template engine, also stores "_flash"
    Public FERR As Hashtable 'for storing form id's with error messages, put to hf("ERR") for parser

    Public db As DB

    Public context As HttpContext
    Public req As HttpRequest
    Public resp As HttpResponse

    Public request_url As String 'current request url (relative to application url)
    Public route As New FwRoute
    Public request_time As TimeSpan 'after dispatch() - total request processing time

    Public cache_control As String = "no-cache" 'cache control header to add to pages, controllers can change per request
    Public is_log_events As Boolean = True 'can be set temporarly to false to prevent event logging (for batch process for ex)

    Public last_error_send_email As String = ""

#Const isSentry = False 'if you use Sentry set to True here, install SentrySDK, in web.config fill endpoint URL to "log_sentry" 
    Private sentryClient As IDisposable

    'begin processing one request
    Public Shared Sub run(Optional context As HttpContext = Nothing)
        If context Is Nothing Then context = HttpContext.Current
        Dim fw As New FW(context)
        FW.Current = fw

        FwHooks.initRequest(fw)
        fw.dispatch()
        FwHooks.finalizeRequest(fw)
        fw.Finalize()
    End Sub

    Public Sub New(context As HttpContext)
        Me.context = context
        req = context.Request
        resp = context.Response

        FwConfig.init(req)

#If isSentry Then
        'Sentry Raven processing
        sentryClient = Sentry.SentrySdk.Init(config("log_sentry"))
        Sentry.SentrySdk.ConfigureScope(Sub(scope) scope.User = New Sentry.Protocol.User With {.Email = SESSION("login")})
#End If

        db = New DB(Me)
        DB.SQL_QUERY_CTR = 0 'reset query counter

        G = config().Clone() 'by default G contains conf

        'per request settings
        G("request_url") = req.RawUrl

        'override default lang with user's lang
        If SESSION("lang") > "" Then
            G("lang") = SESSION("lang")
        End If

        FERR = New Hashtable 'reset errors
        parse_form()

        'save flash to current var and update session as flash is used only for nearest request
        If context.Session("_flash") IsNot Nothing Then
            G("_flash") = context.Session("_flash").Clone()
        End If
        context.Session("_flash") = New Hashtable
    End Sub

    Public Overloads Function SESSION() As HttpSessionState
        Return Me.context.Session
    End Function
    'get session value by name
    'set session value by name - return Me in this case
    Public Overloads Function SESSION(name As String, Optional ByVal value As Object = Nothing) As Object
        If value Is Nothing Then
            If context.Session Is Nothing Then
                logger(LogLevel.ERROR, "CONTEXT SESSION IS Nothing")
                Return Nothing
            End If
            Return context.Session(name)
        Else
            context.Session(name) = value
            Return Me 'for chaining
        End If
    End Function

    'FLASH - used to pass something to the next request (and only on this request)
    Public Overloads Function FLASH() As Hashtable
        Return G("_flash")
    End Function
    'get flash value by name
    'set flash value by name - return FLASH() in this case
    Public Overloads Function FLASH(name As String, Optional ByVal value As Object = Nothing) As Object
        If value Is Nothing Then
            'read mode - return current flash
            Return Me.G("_flash")(name)
        Else
            'write for the next request
            SESSION("_flash")(name) = value
            Return Me 'for chaining
        End If
    End Function

    'return all the settings
    Public Overloads Function config() As Hashtable
        Return FwConfig.settings
    End Function
    'return just particular setting
    Public Overloads Function config(name As String) As Object
        Return FwConfig.settings(name)
    End Function

    ''' <summary>
    ''' returns format expected by client browser
    ''' </summary>
    ''' <returns>"pjax", "json" or empty (usual html page)</returns>
    Public Function get_response_expected_format() As String
        Dim result As String = ""
        If Me.route.format = "json" OrElse Me.req.AcceptTypes IsNot Nothing AndAlso Array.IndexOf(Me.req.AcceptTypes, "application/json") >= Me.req.AcceptTypes.GetLowerBound(0) Then
            result = "json"
        ElseIf Me.route.format = "pjax" OrElse Me.req.Headers("X-Requested-With") > "" Then
            result = "pjax"
        End If
        Return result
    End Function

    ''' <summary>
    ''' return true if browser requests json response
    ''' </summary>
    ''' <returns></returns>
    Public Function isJsonExpected() As Boolean
        Return get_response_expected_format() = "json"
    End Function

    Public Sub getRoute()
        Dim url As String = req.Path
        'cut the App path from the begin
        If req.ApplicationPath > "/" Then url = Replace(url, req.ApplicationPath, "")
        url = Regex.Replace(url, "\/$", "") 'cut last / if any
        Me.request_url = url

        logger(LogLevel.TRACE, "REQUESTING ", url)

        'init defaults
        route = New FwRoute With {
            .controller = "Home",
            .action = "Index",
            .action_raw = "",
            .id = "",
            .action_more = "",
            .format = "html",
            .method = req.RequestType,
            .params = New ArrayList
        }

        'check if method override exits
        If FORM.ContainsKey("_method") Then
            If METHOD_ALLOWED.ContainsKey(FORM("_method")) Then route.method = FORM("_method")
        End If
        If route.method = "HEAD" Then route.method = "GET" 'for website processing HEAD is same as GET, IIS will send just headers


        Dim controller_prefix As String = ""

        'process config special routes (redirects, rewrites)
        Dim routes As Hashtable = Me.config("routes")
        Dim is_routes_found As Boolean = False
        For Each route_key As String In routes.Keys
            If url = route_key Then
                Dim rdest As String = routes(route_key)
                Dim m1 As Match = Regex.Match(rdest, "^(?:(GET|POST|PUT|DELETE) )?(.+)")
                If m1.Success Then
                    'override method
                    If m1.Groups(1).Value > "" Then route.method = m1.Groups(1).Value
                    If m1.Groups(2).Value.Substring(0, 1) = "/" Then
                        'if started from / - this is redirect url
                        url = m1.Groups(2).Value
                    Else
                        'it's a direct class-method to call, no further REST processing required
                        is_routes_found = True
                        Dim sroute As String() = Split(m1.Groups(2).Value, "::", 2)
                        route.controller = Utils.routeFixChars(sroute(0))
                        If UBound(sroute) > 0 Then route.action_raw = sroute(1)
                        Exit For
                    End If
                Else
                    logger(LogLevel.WARN, "Wrong route destination: " & rdest)
                End If
            End If
        Next

        If Not is_routes_found Then
            'TODO move prefix cut to separate func
            Dim prefix_rx As String = FwConfig.getRoutePrefixesRX()
            route.controller_path = ""
            Dim m_prefix As Match = Regex.Match(url, prefix_rx)
            If m_prefix.Success Then
                'convert from /Some/Prefix to SomePrefix
                controller_prefix = Utils.routeFixChars(m_prefix.Groups(1).Value)
                route.controller_path = "/" & controller_prefix
                url = m_prefix.Groups(2).Value
            End If


            'detect REST urls
            ' GET   /controller[/.format]       Index
            ' POST  /controller                 Save     (save new record - Create)
            ' PUT   /controller                 SaveMulti (update multiple records)
            ' GET   /controller/new             ShowForm (show new form - ShowNew)
            ' GET   /controller/{id}[.format]   Show     (show in format - not for editing)
            ' GET   /controller/{id}/edit       ShowForm (show edit form - ShowEdit)
            ' GET   /controller/{id}/delete     ShowDelete
            ' POST/PUT  /controller/{id}        Save     (save changes to exisitng record - Update    Note:Request.Form should contain data
            ' POST/DELETE  /controller/{id}            Delete    Note:Request.Form should NOT contain any data
            '
            ' /controller/(Action)              Action    call for arbitrary action from the controller
            Dim m As Match = Regex.Match(url, "^/([^/]+)(?:/(new|\.\w+)|/([\d\w_-]+)(?:\.(\w+))?(?:/(edit|delete))?)?/?$")
            If m.Success Then
                route.controller = Utils.routeFixChars(m.Groups(1).Value)
                If String.IsNullOrEmpty(route.controller) Then Throw New Exception("Wrong request")

                'capitalize first letter - TODO - URL-case-insensitivity should be an option!
                route.controller = route.controller.Substring(0, 1).ToUpper + route.controller.Substring(1)
                route.id = m.Groups(3).Value
                route.format = m.Groups(4).Value
                route.action_more = m.Groups(5).Value
                If m.Groups(2).Value > "" Then
                    If m.Groups(2).Value = "new" Then
                        route.action_more = "new"
                    Else
                        route.format = m.Groups(2).Value.Substring(1)
                    End If
                End If

                'match to method (GET/POST)
                If route.method = "GET" Then
                    If route.action_more = "new" Then
                        route.action_raw = "ShowForm"
                    ElseIf route.id > "" And route.action_more = "edit" Then
                        route.action_raw = "ShowForm"
                    ElseIf route.id > "" And route.action_more = "delete" Then
                        route.action_raw = "ShowDelete"
                    ElseIf route.id > "" Then
                        route.action_raw = "Show"
                    Else
                        route.action_raw = "Index"
                    End If
                ElseIf route.method = "POST" Then
                    If route.id > "" Then
                        If req.Form.Count > 0 OrElse req.InputStream.Length > 0 Then 'POST form or body payload
                            route.action_raw = "Save"
                        Else
                            route.action_raw = "Delete"
                        End If
                    Else
                        route.action_raw = "Save"
                    End If
                ElseIf route.method = "PUT" Then
                    If route.id > "" Then
                        route.action_raw = "Save"
                    Else
                        route.action_raw = "SaveMulti"
                    End If
                ElseIf route.method = "DELETE" And route.id > "" Then
                    route.action_raw = "Delete"
                Else
                    logger(LogLevel.WARN, "Wrong Route Params")
                    logger(LogLevel.WARN, route.method)
                    logger(LogLevel.WARN, url)
                    err_msg("Wrong Route Params")
                    Exit Sub
                End If

                logger(LogLevel.TRACE, "REST controller.action=", route.controller, ".", route.action_raw)

            Else
                'otherwise detect controller/action/id.format/more_action
                Dim parts As Array = Split(url, "/")
                'logger(parts)
                Dim ub As Integer = UBound(parts)
                If ub >= 1 Then route.controller = Utils.routeFixChars(parts(1))
                If ub >= 2 Then route.action_raw = parts(2)
                If ub >= 3 Then route.id = parts(3)
                If ub >= 4 Then route.action_more = parts(4)
            End If
        End If

        route.controller_path = route.controller_path & "/" & route.controller
        'add controller prefix if any
        route.controller = controller_prefix & route.controller
        route.action = Utils.routeFixChars(route.action_raw)
        If String.IsNullOrEmpty(route.action) Then route.action = "Index"
    End Sub

    Public Sub dispatch()
        Dim start_time As DateTime = DateTime.Now

        Me.getRoute()

        Dim args() As [String] = {route.id} 'TODO - add rest of possible params from parts

        Try
            Dim auth_check_controller = _auth(route.controller, route.action)

            Dim calledType As Type = Type.GetType(route.controller & "Controller", False, True) 'case ignored
            If calledType Is Nothing Then
                logger(LogLevel.DEBUG, "No controller found for controller=[", route.controller, "], using default Home")
                'no controller found - call default controller with default action
                calledType = Type.GetType("HomeController", True)
                route.controller_path = "/Home"
                route.controller = "Home"
                route.action = "NotFound"
            Else
                'controller found
                If auth_check_controller = 1 Then
                    'but need's check access level on controller level
                    Dim field = calledType.GetField("access_level", BindingFlags.Public Or BindingFlags.Static)
                    If field IsNot Nothing Then
                        Dim current_level As Integer = -1
                        If SESSION("access_level") IsNot Nothing Then current_level = SESSION("access_level")

                        If current_level < Utils.f2int(field.GetValue(Nothing)) Then Throw New AuthException("Bad access - Not authorized (2)")
                    End If
                End If
            End If

            logger(LogLevel.TRACE, "TRY controller.action=", route.controller, ".", route.action)

            Dim mInfo As MethodInfo = calledType.GetMethod(route.action & "Action")
            If IsNothing(mInfo) Then
                logger(LogLevel.DEBUG, "No method found for controller.action=[", route.controller, ".", route.action, "], checking route_default_action")
                'no method found - try to get default action
                Dim what_to_do As Boolean = False
                Dim pInfo As FieldInfo = calledType.GetField("route_default_action")
                If pInfo IsNot Nothing Then
                    Dim pvalue As String = pInfo.GetValue(Nothing)
                    If pvalue = "index" Then
                        ' = index - use IndexAction for unknown actions
                        route.action = "Index"
                        mInfo = calledType.GetMethod(route.action & "Action")
                        what_to_do = True
                    ElseIf pvalue = "show" Then
                        ' = show - assume action is id and use ShowAction
                        If route.id > "" Then route.params.Add(route.id) 'route.id is a first param in this case. TODO - add all rest of params from split("/") here
                        If route.action_more > "" Then route.params.Add(route.action_more) 'route.action_more is a second param in this case

                        route.id = route.action_raw
                        args(0) = route.id

                        route.action = "Show"
                        mInfo = calledType.GetMethod(route.action & "Action")
                        what_to_do = True
                    End If
                End If

            End If

            'save to globals so it can be used in templates
            G("controller") = route.controller
            G("action") = route.action
            G("controller.action") = route.controller & "." & route.action

            logger(LogLevel.TRACE, "FINAL controller.action=", route.controller, ".", route.action)
            'logger(LogLevel.TRACE, "route.method=" , route.method)
            'logger(LogLevel.TRACE, "route.controller=" , route.controller)
            'logger(LogLevel.TRACE, "route.action=" , route.action)
            'logger(LogLevel.TRACE, "route.format=" , route.format)
            'logger(LogLevel.TRACE, "route.id=" , route.id)
            'logger(LogLevel.TRACE, "route.action_more=" , route.action_more)

            logger(LogLevel.INFO, "REQUEST START [", route.method, " ", request_url, "] => ", route.controller, ".", route.action)

            If mInfo Is Nothing Then
                'if no method - just call FW.parser(hf) - show template from /route.controller/route.action dir
                logger(LogLevel.DEBUG, "DEFAULT PARSER")
                parser(New Hashtable)
            Else
                call_controller(calledType, mInfo, args)
            End If
            'logger(LogLevel.INFO, "NO EXCEPTION IN dispatch")

        Catch Ex As RedirectException
            'not an error, just exit via Redirect
            logger(LogLevel.INFO, "Redirected...")

        Catch Ex As AuthException 'not authorized for the resource requested
            logger(LogLevel.DEBUG, Ex.Message)
            'if not logged - just redirect to login 
            If SESSION("is_logged") <> True Then
                redirect(config("UNLOGGED_DEFAULT_URL"), False)
            Else
                err_msg(Ex.Message)
            End If

        Catch Ex As ApplicationException

            'get very first exception
            Dim msg As String = Ex.Message
            Dim iex As Exception = Ex
            While iex.InnerException IsNot Nothing
                iex = iex.InnerException
                msg = iex.Message
            End While

            If TypeOf (iex) Is RedirectException Then
                'not an error, just exit via Redirect - TODO - remove here as already handled above?
                logger(LogLevel.DEBUG, "Redirected...")
            ElseIf TypeOf (iex) Is UserException Then
                'no need to log/report detailed user exception
                logger(LogLevel.INFO, "UserException: " & msg)
                err_msg(msg, iex)
            Else
                'it's ApplicationException, so just warning
                logger(LogLevel.WARN, "===== ERROR DUMP APP =====")
                logger(LogLevel.WARN, Ex.Message)
                logger(LogLevel.WARN, Ex.ToString())
                logger(LogLevel.WARN, "REQUEST FORM:", FORM)
                logger(LogLevel.WARN, "SESSION:", SESSION)

                'send_email_admin("App Exception: " & Ex.ToString() & vbCrLf & vbCrLf & _
                '                 "Request: " & req.Path & vbCrLf & vbCrLf & _
                '                 "Form: " & dumper(FORM) & vbCrLf & vbCrLf & _
                '                 "Session:" & dumper(SESSION))

                err_msg(msg, Ex)
            End If

        Catch Ex As Exception
            'it's general Exception, so something more severe occur, log as error and notify admin
            logger(LogLevel.ERROR, "===== ERROR DUMP =====")
            logger(LogLevel.ERROR, Ex.Message)
            logger(LogLevel.ERROR, Ex.ToString())
            logger(LogLevel.ERROR, "REQUEST FORM:", FORM)
            logger(LogLevel.ERROR, "SESSION:", SESSION)

            send_email_admin("Exception: " & Ex.ToString() & vbCrLf & vbCrLf &
                             "Request: " & req.Path & vbCrLf & vbCrLf &
                             "Form: " & dumper(FORM) & vbCrLf & vbCrLf &
                             "Session:" & dumper(SESSION))

            If Me.config("log_level") >= LogLevel.DEBUG Then
                Throw
            Else
                err_msg("Server Error. Please, contact site administrator!", Ex)
            End If
        End Try

        Dim end_timespan As TimeSpan = DateTime.Now - start_time
        logger(LogLevel.INFO, "REQUEST END   [", route.method, " ", request_url, "] in ", end_timespan.TotalSeconds, "s, ", String.Format("{0:0.000}", 1 / end_timespan.TotalSeconds), "/s, ", DB.SQL_QUERY_CTR, " SQL")
    End Sub

    'simple auth check based on /controller/action - and rules filled in in Config class
    'called from Dispatcher
    'throws exception OR if is_die=false
    ' return 2 - if user allowed to see page - explicitly based on fw.config
    ' return 1 - if no fw.config rule, so need to further check Controller.access_level (not checking here for performance reasons)
    ' return 0 - if not allowed
    Public Function _auth(ByVal controller As String, ByVal action As String, Optional is_die As Boolean = True) As Integer
        Dim result As Integer = 0

        'integrated XSS check - only for POST/PUT/DELETE requests 
        ' OR for standard actions: Save, Delete, SaveMulti
        ' OR if it contains XSS param
        If (FORM.ContainsKey("XSS") OrElse route.method = "POST" OrElse route.method = "PUT" OrElse route.method = "DELETE" _
            OrElse action = "Save" OrElse action = "Delete" OrElse action = "SaveMulti") _
            AndAlso SESSION("XSS") > "" AndAlso SESSION("XSS") <> FORM("XSS") Then
            'XSS validation failed - check if we are under xss-excluded controller
            Dim no_xss As Hashtable = Me.config("no_xss")
            If no_xss Is Nothing OrElse Not no_xss.ContainsKey(controller) Then
                If is_die Then Throw New AuthException("XSS Error. Reload the page or try to re-login")
                Return result
            End If
        End If

        Dim path As String = "/" & controller & "/" & action
        Dim path2 As String = "/" & controller

        'pre-check controller's access level by url
        Dim current_level As Integer = -1
        If SESSION("access_level") IsNot Nothing Then current_level = SESSION("access_level")
        Dim rule_level As Integer
        Dim rules As Hashtable = config("access_levels")
        If rules.ContainsKey(path) Then
            If current_level >= rules(path) Then result = 2
        ElseIf rules.ContainsKey(path2) Then
            If current_level >= rules(path2) Then result = 2
        Else
            rule_level = -1 'no restrictions defined for this url in config
            result = 1 'need to check Controller.access_level after _auth
        End If

        If result = 0 AndAlso is_die Then Throw New AuthException("Bad access - Not authorized")
        Return result
    End Function

    'parse query string, form and json in request body into fw.FORM
    Private Sub parse_form()
        Dim input As New Hashtable

        For Each s As String In req.QueryString.Keys
            If s IsNot Nothing Then input(s) = req.QueryString(s)
        Next

        For Each s As String In req.Form.Keys
            If s IsNot Nothing Then input(s) = req.Form(s)
        Next

        'after perpare_FORM - grouping for names like XXX[YYYY] -> FORM{XXX}=@{YYYY1, YYYY2, ...}
        Dim SQ As New Hashtable
        Dim k As String
        Dim sk As String

        Dim f As New Hashtable
        For Each s As String In input.Keys
            Dim m As Match = Regex.Match(s, "^([^\]]+)\[([^\]]+)\]$")
            If m.Groups.Count > 1 Then
                'complex name
                k = m.Groups(1).ToString()
                sk = m.Groups(2).ToString()
                If Not SQ.ContainsKey(k) Then SQ(k) = New Hashtable
                SQ(k)(sk) = input(s)
            Else
                f(s) = input(s)
            End If
        Next

        For Each s As String In SQ.Keys
            f(s) = SQ(s)
        Next

        'also parse json in request body if any
        If req.InputStream.Length > 0 AndAlso Left(req.ContentType, Len("application/json")) = "application/json" Then
            Try
                'also could try this with Utils.json_decode
                req.InputStream.Position = 0
                Dim json = New IO.StreamReader(req.InputStream).ReadToEnd()
                Dim h = New Script.Serialization.JavaScriptSerializer().Deserialize(Of Hashtable)(json)
                logger(LogLevel.TRACE, "REQUESTED JSON:", h)

                Utils.mergeHash(f, h)
            Catch ex As Exception
                logger(LogLevel.WARN, "Request JSON parse error")
            End Try
        End If

        'logger(f)
        FORM = f
    End Sub

    Public Overloads Sub logger(ByVal ParamArray args() As Object)
        If args.Length = 0 Then Return
        _logger(LogLevel.DEBUG, args)
    End Sub
    Public Overloads Sub logger(level As LogLevel, ByVal ParamArray args() As Object)
        If args.Length = 0 Then Return
        _logger(level, args)
    End Sub

    'internal logger routine, just to avoid pass args by value 2 times
    Public Sub _logger(level As LogLevel, ByRef args() As Object)
        'skip logging if requested level more than config's debug level
        If level > CType(Me.config("log_level"), LogLevel) Then Return

        Dim str As New StringBuilder(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
        str.Append(" ").Append(level.ToString()).Append(" ")
        str.Append(Diagnostics.Process.GetCurrentProcess().Id).Append(" ")
        Dim st As New Diagnostics.StackTrace(True)

        Try
            Dim i = 1
            Dim sf As Diagnostics.StackFrame = st.GetFrame(i)
            'skip logger methods and DB internals as we want to know line where logged thing actually called from
            While sf.GetMethod().Name = "logger" OrElse Right(sf.GetFileName() & "", 6) = "\DB.vb"
                i += 1
                sf = st.GetFrame(i)
            End While
            Dim fname As String = sf.GetFileName()
            If fname IsNot Nothing Then 'nothing in Release configuration
                str.Append(Replace(Replace(fname.ToString(), Me.config("site_root"), ""), "\App_Code", ""))
            End If
            str.Append(":").Append(sf.GetMethod().Name).Append(" ").Append(sf.GetFileLineNumber().ToString).Append(" # ")
        Catch ex As Exception
            str.Append(" ... #")
        End Try

        For Each dmp_obj As Object In args
            str.Append(dumper(dmp_obj))
        Next

        'write to debug console first
        Diagnostics.Debug.WriteLine(str)

        'write to log file
        Dim log_file As String = config("log")
        Try
            'keep log file open to avoid overhead
            If floggerFS Is Nothing Then
                'open log with shared read/write so loggers from other processes can still write to it
                floggerFS = New FileStream(log_file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
                floggerSW = New System.IO.StreamWriter(floggerFS) With {
                    .AutoFlush = True
                }
            End If
            'force seek to end just in case other process added to file
            floggerFS.Seek(0, SeekOrigin.End)
            floggerSW.WriteLine(str.ToString)
        Catch ex As Exception
            Diagnostics.Debug.WriteLine("WARN logger can't write to log file. Reason:" & ex.Message)
        End Try

#If isSentry Then
        'Sentry - if INFO, WARN, ERROR, FATAL - add trail
        If level <= LogLevel.INFO Then
            'raven error level is -1 from fw level
            'SharpRaven.Data.BreadcrumbLevel.Info
            'SharpRaven.Data.BreadcrumbLevel.Warning
            'SharpRaven.Data.BreadcrumbLevel.Error
            'SharpRaven.Data.BreadcrumbLevel.Critical
            Sentry.SentrySdk.AddBreadcrumb(str.ToString, Nothing, Nothing, Nothing, level - 1)
        End If
#End If

    End Sub

    Public Shared Function dumper(ByVal dmp_obj As Object, Optional ByVal level As Integer = 0) As String 'TODO better type detection(suitable for all collection types)
        Dim str As New StringBuilder
        If dmp_obj Is Nothing Then Return "[Nothing]"
        If level > 10 Then Return "[Too Much Recursion]"

        Try
            Dim type As Type = dmp_obj.GetType()
            Dim typeCode As TypeCode = Type.GetTypeCode(type)
            Dim intend As String = New StringBuilder().Insert(0, "    ", level).Append(" ").ToString()

            level += 1
            If typeCode.ToString = "Object" Then
                str.Append(vbCrLf)
                If TypeOf (dmp_obj) Is IList Then 'ArrayList
                    str.Append(intend & "[" & vbCrLf)
                    Dim v As Object
                    For Each v In dmp_obj
                        str.Append(intend & " " & dumper(v, level) & vbCrLf)
                    Next v
                    str.Append(intend & "]" & vbCrLf)
                ElseIf TypeOf (dmp_obj) Is ICollection Then 'Hashtable
                    str.Append(intend & "{" & vbCrLf)
                    Dim k As Object
                    For Each k In dmp_obj.keys
                        str.Append(intend & " " & k & " => " & dumper(dmp_obj(k), level) & vbCrLf)
                    Next k
                    str.Append(intend & "}" & vbCrLf)
                Else
                    str.Append(intend & type.ToString & "==" & typeCode.ToString & vbCrLf)
                End If
            Else
                str.Append(dmp_obj.ToString())
            End If
        Catch ex As Exception
            str.Append("***cannot dump object***")
        End Try

        Return str.ToString()
    End Function

    'return file content OR "" if no file exists or some other error happened (see errorInfo)
    ''' <summary>
    ''' return file content OR ""
    ''' </summary>
    ''' <param name="filename"></param>
    ''' <param name="errInfo"></param>
    ''' <returns></returns>
    Public Shared Function get_file_content(ByRef filename As String, Optional ByRef errInfo As String = "") As String
        Dim result As String = ""
        filename = Regex.Replace(filename, "/", "\")
        If Not File.Exists(filename) Then Return result

        Try
            result = File.ReadAllText(filename)
        Catch Ex As Exception
            'TODO logger("ERROR", "Error getting file content [" & file_name & "]")
            errInfo = Ex.Message
        End Try
        Return result
    End Function

    ''' <summary>
    ''' return array of file lines OR empty array if no file exists or some other error happened (see errorInfo)
    ''' </summary>
    ''' <param name="filename"></param>
    ''' <param name="errInfo"></param>
    ''' <returns></returns>
    Public Shared Function get_file_lines(ByRef filename As String, Optional ByRef errInfo As String = "") As String()
        Dim result As String() = Array.Empty(Of String)()
        Try
            result = File.ReadAllLines(filename)
        Catch ex As Exception
            'TODO logger("ERROR", "Error getting file content [" & file_name & "]")
            errInfo = ex.Message
        End Try
        Return result
    End Function

    ''' <summary>
    ''' replace or append file content
    ''' </summary>
    ''' <param name="filename"></param>
    ''' <param name="fileData"></param>
    ''' <param name="isAppend">False by default </param>
    Public Shared Sub set_file_content(ByRef filename As String, ByRef fileData As String, Optional ByRef isAppend As Boolean = False)
        filename = Regex.Replace(filename, "/", "\")

        Using sw As New StreamWriter(filename, isAppend)
            sw.Write(fileData)
        End Using
    End Sub

    'show page from template  /route.controller/route.action = parser('/route.controller/route.action/', $ps)
    Public Overloads Sub parser(hf As Hashtable)
        Me.parser(LCase(route.controller_path & "/" & route.action), hf)
    End Sub

    'same as parsert(hf), but with base dir param
    'output format based on requested format: json, pjax or (default) full page html
    'for automatic json response support - set hf("_json") = True OR set hf("_json")=ArrayList/Hashtable - if json requested, only _json content will be returned
    'to override page template - set hf("_layout")="another_page_layout.html" (relative to SITE_TEMPLATES dir)
    '(not for json) to perform route_redirect - set hf("_route_redirect")("method"), hf("_route_redirect")("controller"), hf("_route_redirect")("args")
    '(not for json) to perform redirect - set hf("_redirect")="url"
    'TODO - create another func and call it from call_controller for processing _redirect, ... (non-parsepage) instead of calling parser?
    Public Overloads Sub parser(ByVal bdir As String, hf As Hashtable)
        Me.resp.CacheControl = cache_control

        Dim format As String = Me.get_response_expected_format()
        If format = "json" Then
            If hf.ContainsKey("_json") Then
                If TypeOf hf("_json") Is Boolean AndAlso hf("_json") = True Then
                    hf.Remove("_json") 'remove internal flag
                    Me.parser_json(hf)
                Else
                    Me.parser_json(hf("_json")) 'if _json exists - return only this element content
                End If
            Else
                Dim ps As New Hashtable From {
                    {"success", False},
                    {"message", "JSON response is not enabled for this Controller.Action (set ps(""_json"")=True or ps(""_json"")=data... to enable)."}
                }
                Me.parser_json(ps)
            End If
            Return 'no further processing for json
        End If

        If hf.ContainsKey("_route_redirect") Then
            Dim rr = hf("_route_redirect")
            Me.routeRedirect(rr("method"), rr("controller"), rr("args"))
            Return 'no further processing
        End If

        If hf.ContainsKey("_redirect") Then
            Me.redirect(hf("_redirect"))
            Return 'no further processing
        End If

        If Me.FERR.Count > 0 AndAlso Not hf.ContainsKey("ERR") Then hf("ERR") = Me.FERR 'add errors if any

        Dim layout As String
        If format = "pjax" Then
            layout = G("PAGE_LAYOUT_PJAX")
        Else
            layout = G("PAGE_LAYOUT")
        End If

        If hf.ContainsKey("_layout") Then layout = hf("_layout")
        _parser(bdir, layout, hf)
    End Sub

    '- show page from template  /controller/action = parser('/controller/action/', $layout, $ps)
    Public Overloads Sub parser(ByVal bdir As String, ByVal tpl_name As String, ByVal hf As Hashtable)
        hf("_layout") = tpl_name
        parser(bdir, hf)
    End Sub

    'actually uses ParsePage
    Public Sub _parser(ByVal bdir As String, ByVal tpl_name As String, ByVal hf As Hashtable)
        logger(LogLevel.DEBUG, "parsing page bdir=", bdir, ", tpl=", tpl_name)
        Dim parser_obj As New ParsePage(Me)
        Dim page As String = parser_obj.parse_page(bdir, tpl_name, hf)
        resp.Write(page)
    End Sub

    Public Sub parser_json(ByVal hf As Object)
        Dim parser_obj As New ParsePage(Me)
        Dim page As String = parser_obj.parse_json(hf)
        resp.AddHeader("Content-type", "application/json; charset=utf-8")
        resp.Write(page)
    End Sub

    'perform redirect
    'if is_exception=True (default) - throws RedirectException, so current request processing can end early
    Public Sub redirect(ByVal url As String, Optional is_exception As Boolean = True)
        If Regex.IsMatch(url, "^/") Then url = Me.config("ROOT_URL") & url
        resp.Redirect(url, False)
        If is_exception Then Throw New RedirectException
    End Sub

    Public Overloads Sub routeRedirect(ByVal action As String, ByVal controller As String, Optional ByVal args As Object = Nothing)
        setController(IIf(controller > "", controller, route.controller), action)

        Dim calledType As Type = Type.GetType(route.controller & "Controller", True)
        Dim mInfo As MethodInfo = calledType.GetMethod(route.action & "Action")
        If IsNothing(mInfo) Then
            logger(LogLevel.INFO, "No method found for controller.action=[", route.controller, ".", route.action, "], displaying static page from related templates")
            'no method found - set to default Index method
            'route.action = "Index"
            'mInfo = calledType.GetMethod(route.action & "Action")

            'if no method - show template from /route.controller/route.action dir
            parser("/" & LCase(route.controller) & "/" & LCase(route.action), New Hashtable)
        End If

        If mInfo IsNot Nothing Then
            call_controller(calledType, mInfo, args)
        End If

    End Sub
    'same as above just with default controller
    Public Overloads Sub routeRedirect(ByVal action As String, Optional ByVal args As Object = Nothing)
        routeRedirect(action, route.controller, args)
    End Sub

    ''' <summary>
    ''' set route.controller and optionally route.action, updates G too
    ''' </summary>
    ''' <param name="controller"></param>
    ''' <param name="action"></param>
    Public Sub setController(controller As String, Optional action As String = "")
        route.controller = controller
        If action > "" Then route.action = action

        G("controller") = route.controller
        G("action") = route.action
        G("controller.action") = route.controller & "." & route.action
        'TODO set route.controller_path too?
    End Sub

    'Call controller
    Public Sub call_controller(calledType As Type, mInfo As MethodInfo, Optional ByVal args As Object = Nothing)
        'check if method accept agrs and don't pass args if no args expected
        Dim params() As System.Reflection.ParameterInfo = mInfo.GetParameters()
        If params.Length = 0 Then args = Nothing

        Dim new_controller As FwController = Activator.CreateInstance(calledType)
        new_controller.init(Me)
        Dim ps As Hashtable = Nothing
        Try
            ps = mInfo.Invoke(new_controller, args)
        Catch ex As TargetInvocationException
            'ignore redirect exception
            If ex.InnerException Is Nothing OrElse Not (TypeOf (ex.InnerException) Is RedirectException) Then
                Throw 'this keeps stack, also see http://weblogs.asp.net/fmarguerie/rethrowing-exceptions-and-preserving-the-full-call-stack-trace
            End If
            'Throw ex.InnerException
        End Try
        If ps IsNot Nothing Then parser(ps)
    End Sub


    Public Sub file_response(ByVal filepath As String, ByVal attname As String, Optional ContentType As String = "application/octet-stream", Optional ContentDisposition As String = "attachment")
        logger(LogLevel.DEBUG, "sending file response  = ", filepath, " as ", attname)
        attname = Regex.Replace(attname, "[^\w. \-]+", "_")
        resp.AppendHeader("Content-type", ContentType)
        resp.AppendHeader("Content-Length", Utils.fileSize(filepath))
        resp.AppendHeader("Content-Disposition", ContentDisposition & "; filename=""" & attname & """")
        resp.TransmitFile(filepath)
        resp.OutputStream.Close()
        'resp.End() 'Causing Thread was being aborted exception
    End Sub

    'SEND EMAIL
    'mail_to may contain several emails delimited by ;
    'filenames (optional) - human filename => hash filepath
    'aCC - arraylist of CC addresses (strings)
    'reply_to - optional reply to email
    'options - hashtable with options: 
    '         "read-receipt"
    'RETURN:
    ' true if sent successfully
    ' false if some problem occured (see log)
    Public Function send_email(ByVal mail_from As String, ByVal mail_to As String, ByVal mail_subject As String, ByVal mail_body As String, Optional filenames As Hashtable = Nothing, Optional aCC As ArrayList = Nothing, Optional reply_to As String = "", Optional options As Hashtable = Nothing) As Boolean
        Dim result As Boolean = True
        Dim message As MailMessage = Nothing
        If options Is Nothing Then options = New Hashtable

        Try
            If Len(mail_from) = 0 Then mail_from = Me.config("mail_from") 'default mail from
            mail_subject = Regex.Replace(mail_subject, "[\r\n]+", " ")

            If Me.config("is_test") Then
                Dim test_email As String = Me.config("test_email")
                mail_body = "TEST SEND. PASSED MAIL_TO=[" & mail_to & "]" & vbCrLf & mail_body
                mail_to = Me.config("test_email")
                logger(LogLevel.INFO, "EMAIL SENT TO TEST EMAIL [", mail_to, "] - TEST ENABLED IN web.config")
            End If

            logger(LogLevel.INFO, "Sending email. From=[", mail_from, "], ReplyTo=[", reply_to, "], To=[", mail_to, "], Subj=[", mail_subject, "]")
            logger(LogLevel.DEBUG, mail_body)

            If mail_to > "" Then

                message = New MailMessage
                If options.ContainsKey("read-receipt") Then message.Headers.Add("Disposition-Notification-To", mail_from)

                'detect HTML body - if it's started with <!DOCTYPE or <html tags
                If Regex.IsMatch(mail_body, "^\s*<(!DOCTYPE|html)[^>]*>", RegexOptions.IgnoreCase) Then
                    message.IsBodyHtml = True
                End If

                message.From = New MailAddress(mail_from)
                message.Subject = mail_subject
                message.Body = mail_body
                'If reply_to > "" Then message.ReplyTo = New MailAddress(reply_to) '.net<4
                If reply_to > "" Then message.ReplyToList.Add(reply_to) '.net>=4

                'mail_to may contain several emails delimited by ;
                Dim amail_to As ArrayList = Utils.splitEmails(mail_to)
                For Each email As String In amail_to
                    email = Trim(email)
                    If String.IsNullOrEmpty(email) Then Continue For
                    message.To.Add(New MailAddress(email))
                Next

                'add CC if any
                If Not IsNothing(aCC) Then
                    If Me.config("is_test") Then
                        For Each cc As String In aCC
                            logger(LogLevel.INFO, "TEST SEND. PASSED CC=[", cc, "]")
                            message.CC.Add(New MailAddress(mail_to))
                        Next
                    Else
                        For Each cc As String In aCC
                            cc = Trim(cc)
                            If String.IsNullOrEmpty(cc) Then Continue For
                            message.CC.Add(New MailAddress(cc))
                        Next
                    End If
                End If

                'attach attachments if any
                If Not IsNothing(filenames) Then
                    'sort by human name
                    Dim fkeys As New ArrayList(filenames.Keys)
                    fkeys.Sort()
                    For Each human_filename As String In fkeys
                        Dim filename As String = filenames(human_filename)
                        Dim att As New Net.Mail.Attachment(filename, System.Net.Mime.MediaTypeNames.Application.Octet) With {
                            .Name = human_filename,
                            .NameEncoding = System.Text.Encoding.UTF8
                        }
                        'att.ContentDisposition.FileName = human_filename
                        logger(LogLevel.DEBUG, "attachment ", human_filename, " => ", filename)
                        message.Attachments.Add(att)
                    Next
                End If

                Using client As New SmtpClient()
                    client.Send(message)
                    'client.SendAsync(message,"") 'async alternative TBD
                End Using
            End If

        Catch ex As Exception
            result = False
            last_error_send_email = ex.Message
            If ex.InnerException IsNot Nothing Then last_error_send_email &= " " & ex.InnerException.Message
            logger(LogLevel.ERROR, "send_email error:", last_error_send_email)
        Finally
            If message IsNot Nothing Then message.Dispose() 'important, as this will close any opened attachment files
        End Try
        Return result
    End Function

    'shortcut for send_email from template from the /emails template dir
    Public Function send_email_tpl(ByVal mail_to As String, ByVal tpl As String, ByVal hf As Hashtable, Optional filenames As Hashtable = Nothing, Optional aCC As ArrayList = Nothing, Optional reply_to As String = "") As Boolean
        Dim parser_obj As ParsePage = New ParsePage(Me)
        Dim r As Regex = New Regex("[\n\r]+")
        Dim subj_body As String = parser_obj.parse_page("/emails", tpl, hf)
        If subj_body.Length = 0 Then Throw New ApplicationException("No email template defined [" & tpl & "]")
        Dim arr() As String = r.Split(subj_body, 2)
        Return send_email("", mail_to, arr(0), arr(1), filenames, aCC, reply_to)
    End Function

    'send email message to site admin (usually used in case of errors)
    Public Sub send_email_admin(msg As String)
        Me.send_email("", Me.config("admin_email"), Left(msg, 512), msg)
    End Sub

    Public Function load_url(ByVal url As String, Optional params As Hashtable = Nothing) As String
        Dim client As System.Net.WebClient = New System.Net.WebClient
        Dim content As String
        If params IsNot Nothing Then
            'POST
            Dim nv As New NameValueCollection()
            For Each key In params.Keys
                nv.Add(key, params(key))
            Next
            content = (New Text.UTF8Encoding).GetString(client.UploadValues(url, "POST", nv))
        Else
            'GET
            content = client.DownloadString(url)
        End If

        Return content
    End Function

    Public Sub err_msg(ByVal msg As String, Optional Ex As Exception = Nothing)
        Dim hf As Hashtable = New Hashtable
        Dim tpl_dir = "/error"

#If isSentry Then
        'Sentry logging
        Sentry.SentrySdk.CaptureException(Ex)
#End If

        hf("err_time") = Now()
        hf("err_msg") = msg
        If Utils.f2bool(Me.config("IS_DEV")) Then
            hf("is_dump") = True
            If Ex IsNot Nothing Then
                hf("DUMP_STACK") = Ex.ToString()
            End If
            hf("DUMP_FORM") = dumper(FORM)
            hf("DUMP_SESSION") = dumper(SESSION)
        End If

        hf("success") = False
        hf("message") = msg
        hf("_json") = True

        If TypeOf Ex Is ApplicationException Then
            Me.resp.StatusCode = 500
        ElseIf TypeOf Ex Is UserException Then
            Me.resp.StatusCode = 403
            'TBD tpl_dir &= "/client"
        End If

        parser(tpl_dir, hf)
    End Sub

    'return model object by type
    'CACHED in fw.models, so it's singletones
    Public Overloads Function model(Of T As New)() As T
        Dim tt As Type = GetType(T)
        If Not models.ContainsKey(tt.Name) Then
            Dim m As New T()

            'initialize
            GetType(T).GetMethod("init").Invoke(m, New Object() {Me})

            models(tt.Name) = m
        End If
        Return models(tt.Name)
    End Function

    'return model object by model name
    Public Overloads Function model(model_name As String) As FwModel
        If Not models.ContainsKey(model_name) Then
            Dim m As FwModel = Activator.CreateInstance(Type.GetType(model_name))
            'initialize
            m.init(Me)
            models(model_name) = m
        End If
        Return models(model_name)
    End Function

    Public Sub logEvent(ev_icode As String, Optional item_id As Integer = 0, Optional item_id2 As Integer = 0, Optional iname As String = "", Optional records_affected As Integer = 0, Optional changed_fields As Hashtable = Nothing)
        If Not is_log_events Then Return
        Me.model(Of FwEvents).log(ev_icode, item_id, item_id2, iname, records_affected, changed_fields)
    End Sub

    Public Sub rw(ByVal str As String)
        Me.resp.Write(str)
        Me.resp.Write("<br>" & vbCrLf)
        Me.resp.FlushAsync()
    End Sub


#Region "IDisposable Support"
    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                'dispose managed state (managed objects).
                If sentryClient IsNot Nothing Then sentryClient.Dispose()
            End If

            'free unmanaged resources (unmanaged objects) and override Finalize() below.
            Try
                db.Dispose() 'this will return db connections to pool

                Dim log_length = 0
                If floggerFS IsNot Nothing Then log_length = floggerFS.Length

                If floggerSW IsNot Nothing Then floggerSW.Close() 'no need to close floggerFS as StreamWriter closes it
                If floggerFS IsNot Nothing Then
                    floggerFS.Close()

                    'check if log file too large and need to be rotated
                    Dim max_log_size = Utils.f2int(config("log_max_size"))
                    If max_log_size > 0 AndAlso log_length > max_log_size Then
                        Dim to_path = config("log") & ".1"
                        File.Delete(to_path)
                        File.Move(config("log"), to_path)
                    End If
                End If
                ' TODO: set large fields to null.
            Catch ex As Exception
                Diagnostics.Debug.WriteLine("exception in Dispose:" & ex.Message())
            End Try
        End If
        disposedValue = True
    End Sub

    ' override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    Protected Overrides Sub Finalize()
        ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(False)
        MyBase.Finalize()
    End Sub

    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(True)
        ' uncomment the following line if Finalize() is overridden above.
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
