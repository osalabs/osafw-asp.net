' Framework core class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO
Imports System.Net.Mail
Imports System.Reflection

'Custom Exceptions
<Serializable> Public Class AuthException : Inherits ApplicationException
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class
<Serializable> Public Class ValidationException : Inherits ApplicationException
End Class
<Serializable> Public Class RedirectException : Inherits Exception
End Class

'Main Framework Class
Public Class FW
    Implements IDisposable
    Public Shared METHOD_ALLOWED As Hashtable = Utils.qh("GET POST PUT DELETE")

    Private floggerFS As System.IO.FileStream
    Private floggerSW As System.IO.StreamWriter
    Private models As New Hashtable
    Public Shared Current As FW 'store FW current "singleton", set in run

    Public FORM As Hashtable
    Public G As Hashtable 'for storing global vars - used in template engine, also stores "_flash"
    Public FERR As Hashtable 'for storing form id's with error messages, put to hf("ERR") for parser

    Public db As DB

    Public context As HttpContext
    Public req As HttpRequest
    Public resp As HttpResponse

    Public request_url As String 'current request url (relative to application url)
    Public cur_controller_path As String 'store /Prefix/Controller - to use in parser a default path for templates
    Public cur_method As String
    Public cur_controller As String
    Public cur_action As String
    Public cur_id As String
    Public cur_action_more As String
    Public cur_format As String
    Public cur_params As ArrayList

    'begin processing one request
    Public Shared Sub run(Optional context As HttpContext = Nothing)
        If context Is Nothing Then context = HttpContext.Current
        Dim fw As New FW(context)
        FW.Current = fw

        FwHooks.request_init(fw)
        fw.dispatch()
        fw.Finalize()
    End Sub

    Public Sub New(context As HttpContext)
        Me.context = context
        req = context.Request
        resp = context.Response
        FwConfig.init(req)

        db = New DB(Me)

        G = config().Clone() 'by default G contains conf
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

    'return pjax, json or empty (usual html page)
    Public Function get_response_expected_format() As String
        Dim result As String = ""
        If Me.cur_format = "json" OrElse Me.req.AcceptTypes IsNot Nothing AndAlso Array.IndexOf(Me.req.AcceptTypes, "application/json") >= Me.req.AcceptTypes.GetLowerBound(0) Then
            result = "json"
        ElseIf Me.cur_format = "pjax" OrElse Me.req.Headers("X-Requested-With") > "" Then
            result = "pjax"
        End If
        Return result
    End Function

    Public Sub dispatch()
        Dim start_time As DateTime = DateTime.Now

        Dim url As String = req.Path
        'cut the App path from the begin
        If req.ApplicationPath > "/" Then url = Replace(url, req.ApplicationPath, "")
        url = Regex.Replace(url, "\/$", "") 'cut last / if any
        Me.request_url = url

        'init defaults
        cur_controller = "Home"
        cur_action = "Index"
        cur_id = ""
        cur_action_more = ""
        cur_format = "html"
        cur_method = req.RequestType
        cur_params = New ArrayList

        logger("INFO", "*** REQUEST START " & cur_method & " " & url)

        'check if method override exits
        If FORM.ContainsKey("_method") Then
            If METHOD_ALLOWED.ContainsKey(FORM("_method")) Then cur_method = FORM("_method")
        End If
        If cur_method = "HEAD" Then cur_method = "GET" 'for website processing HEAD is same as GET, IIS will send just headers

        Dim cur_action_raw As String = ""
        Dim controller_prefix As String = ""

        'process config special routes (redirects, rewrites)
        Dim routes As Hashtable = Me.config("routes")
        Dim is_routes_found As Boolean = False
        For Each route As String In routes.Keys
            If url = route Then
                Dim rdest As String = routes(route)
                Dim m1 As Match = Regex.Match(rdest, "^(?:(GET|POST|PUT|DELETE) )?(.+)")
                If m1.Success Then
                    'override method
                    If m1.Groups(1).Value > "" Then cur_method = m1.Groups(1).Value
                    If m1.Groups(2).Value.Substring(0, 1) = "/" Then
                        'if started from / - this is redirect url
                        url = m1.Groups(2).Value
                    Else
                        'it's a direct class-method to call, no further REST processing required
                        is_routes_found = True
                        Dim sroute As String() = Split(m1.Groups(2).Value, "::", 2)
                        cur_controller = Utils.route_fix_chars(sroute(0))
                        If UBound(sroute) > 0 Then cur_action_raw = sroute(1)
                        Exit For
                    End If
                Else
                    logger("WARN", "Wrong route destination: " & rdest)
                End If
            End If
        Next

        If Not is_routes_found Then
            'TODO move prefix cut to separate func
            Dim prefix_rx As String = FwConfig.get_route_prefixes_rx()
            cur_controller_path = ""
            Dim m_prefix As Match = Regex.Match(url, prefix_rx)
            If m_prefix.Success Then
                'convert from /Some/Prefix to SomePrefix
                controller_prefix = Utils.route_fix_chars(m_prefix.Groups(1).Value)
                cur_controller_path = "/" & controller_prefix
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
                cur_controller = Utils.route_fix_chars(m.Groups(1).Value)
                If cur_controller = "" Then Throw New Exception("Wrong request")

                'capitalize first letter - TODO - URL-case-insensitivity should be an option!
                cur_controller = cur_controller.Substring(0, 1).ToUpper + cur_controller.Substring(1)
                cur_id = m.Groups(3).Value
                cur_format = m.Groups(4).Value
                cur_action_more = m.Groups(5).Value
                If m.Groups(2).Value > "" Then
                    If m.Groups(2).Value = "new" Then
                        cur_action_more = "new"
                    Else
                        cur_format = m.Groups(2).Value.Substring(1)
                    End If
                End If

                'match to method (GET/POST)
                If cur_method = "GET" Then
                    If cur_action_more = "new" Then
                        cur_action_raw = "ShowForm"
                    ElseIf cur_id > "" And cur_action_more = "edit" Then
                        cur_action_raw = "ShowForm"
                    ElseIf cur_id > "" And cur_action_more = "delete" Then
                        cur_action_raw = "ShowDelete"
                    ElseIf cur_id > "" Then
                        cur_action_raw = "Show"
                    Else
                        cur_action_raw = "Index"
                    End If
                ElseIf cur_method = "POST" Then
                    If cur_id > "" Then
                        If req.Form.Count > 0 Then
                            cur_action_raw = "Save"
                        Else
                            cur_action_raw = "Delete"
                        End If
                    Else
                        cur_action_raw = "Save"
                    End If
                ElseIf cur_method = "PUT" Then
                    If cur_id > "" Then
                        cur_action_raw = "Save"
                    Else
                        cur_action_raw = "SaveMulti"
                    End If
                ElseIf cur_method = "DELETE" And cur_id > "" Then
                    cur_action_raw = "Delete"
                Else
                    logger("ERROR", "Wrong Route Params")
                    logger("ERROR", cur_method)
                    logger("ERROR", url)
                    err_msg("Wrong Route Params")
                    Exit Sub
                End If

                logger("***** REST controller.action=" & cur_controller & "." & cur_action_raw)

            Else
                'otherwise detect controller/action/id.format/more_action
                Dim parts As Array = Split(url, "/")
                'logger(parts)
                Dim ub As Integer = UBound(parts)
                If ub >= 1 Then cur_controller = Utils.route_fix_chars(parts(1))
                If ub >= 2 Then cur_action_raw = parts(2)
                If ub >= 3 Then cur_id = parts(3)
                If ub >= 4 Then cur_action_more = parts(4)
            End If
        End If

        cur_controller_path = cur_controller_path & "/" & cur_controller
        'add controller prefix if any
        cur_controller = controller_prefix & cur_controller
        cur_action = Utils.route_fix_chars(cur_action_raw)
        If cur_action = "" Then cur_action = "Index"

        Dim args() As [String] = {cur_id} 'TODO - add rest of possible params from parts

        Try
            _auth(cur_controller, cur_action)

            Dim calledType As Type = Type.GetType(cur_controller & "Controller", False)
            If calledType Is Nothing Then
                logger("WARN", "No controller found for controller=[" & cur_controller & "], using default Home")
                'no controller found - call default controller with default action
                calledType = Type.GetType("HomeController", True)
                cur_controller_path = "/Home"
                cur_controller = "Home"
                cur_action = "NotFound"
            End If

            logger("***** TRY controller.action=" & cur_controller & "." & cur_action)

            Dim mInfo As MethodInfo = calledType.GetMethod(cur_action & "Action")
            If IsNothing(mInfo) Then
                logger("WARN", "No method found for controller.action=[" & cur_controller & "." & cur_action & "], checking route_default_action")
                'no method found - try to get default action
                Dim what_to_do As Boolean = False
                Dim pInfo As FieldInfo = calledType.GetField("route_default_action")
                If pInfo IsNot Nothing Then
                    Dim pvalue As String = pInfo.GetValue(Nothing)
                    If pvalue = "index" Then
                        ' = index - use IndexAction for unknown actions
                        cur_action = "Index"
                        mInfo = calledType.GetMethod(cur_action & "Action")
                        what_to_do = True
                    ElseIf pvalue = "show" Then
                        ' = show - assume action is id and use ShowAction
                        If cur_id > "" Then cur_params.Add(cur_id) 'cur_id is a first param in this case. TODO - add all rest of params from split("/") here
                        If cur_action_more > "" Then cur_params.Add(cur_action_more) 'cur_action_more is a second param in this case

                        cur_id = cur_action_raw
                        args(0) = cur_id

                        cur_action = "Show"
                        mInfo = calledType.GetMethod(cur_action & "Action")
                        what_to_do = True
                    End If
                End If

            End If

            'save to globals so it can be used in templates
            G("controller") = cur_controller
            G("action") = cur_action
            G("controller.action") = cur_controller & "." & cur_action
            logger("***** FINAL controller.action=" & G("controller.action"))

            'logger("cur_method=" & cur_method)
            'logger("cur_controller=" & cur_controller)
            'logger("cur_action=" & cur_action)
            'logger("cur_format=" & cur_format)
            'logger("cur_id=" & cur_id)
            'logger("cur_action_more=" & cur_action_more)

            If mInfo Is Nothing Then
                'if no method - just call FW.parser(hf) - show template from /cur_controller/cur_action dir
                logger("***** DEFAULT PARSER ")
                parser(New Hashtable)
            Else
                call_controller(calledType, mInfo, args)
            End If
            'logger("INFO", "NO EXCEPTION IN dispatch")

        Catch Ex As RedirectException
            'not an error, just exit via Redirect
            logger("DEBUG", "Redirected...")

        Catch Ex As AuthException 'not authorized for the resource requested
            logger("DEBUG", Ex.Message)
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
                logger("DEBUG", "Redirected...")
            Else
                logger("ERROR", "===== ERROR DUMP APP =====")
                logger("ERROR", Ex.Message)
                logger("ERROR", Ex.ToString())
                logger("ERROR", FORM)
                logger("ERROR", SESSION)

                'send_email_admin("App Exception: " & Ex.ToString() & vbCrLf & vbCrLf & _
                '                 "Request: " & req.Path & vbCrLf & vbCrLf & _
                '                 "Form: " & dumper(FORM) & vbCrLf & vbCrLf & _
                '                 "Session:" & dumper(SESSION))

                err_msg(msg, Ex)
            End If

        Catch Ex As Exception
            logger("ERROR", "===== ERROR DUMP =====")
            logger("ERROR", Ex.Message)
            logger("ERROR", Ex.ToString())
            logger("ERROR", FORM)
            logger("ERROR", SESSION)

            send_email_admin("Exception: " & Ex.ToString() & vbCrLf & vbCrLf & _
                             "Request: " & req.Path & vbCrLf & vbCrLf & _
                             "Form: " & dumper(FORM) & vbCrLf & vbCrLf & _
                             "Session:" & dumper(SESSION))

            If Utils.f2bool(Me.config("is_debug")) Then
                Throw
            Else
                err_msg("Server Error. Please, contact site administrator!", Ex)
            End If
        End Try

        Dim end_timespan As TimeSpan = DateTime.Now - start_time
        logger("INFO", "*** REQUEST END in " & end_timespan.TotalSeconds & "s, " & String.Format("{0:0.000}", 1 / end_timespan.TotalSeconds) & "/s")
    End Sub

    'simple auth check based on /controller/action - and rules filled in in Config class
    'called from Dispatcher
    'throws exception OR if is_die=false
    ' return true - if user allowed to see page
    ' return false - if not allowed
    Public Function _auth(ByVal controller As String, ByVal action As String, Optional is_die As Boolean = True) As Boolean
        Dim result As Boolean = False

        'integrated XSS check - only for POST/PUT/DELETE requests or if it contains XSS param
        If (FORM.ContainsKey("XSS") OrElse cur_method = "POST" OrElse cur_method = "PUT" OrElse cur_method = "DELETE") _
            AndAlso SESSION("XSS") > "" AndAlso SESSION("XSS") <> FORM("XSS") _
            AndAlso controller <> "Login" Then 'special case - no XSS check for Login controller!
            If is_die Then Throw New AuthException("XSS Error. Reload the page or try to re-login")
            Return False
        End If

        Dim path As String = "/" & controller & "/" & action
        Dim path2 As String = "/" & controller

        Dim current_level As Integer = -1
        If SESSION("access_level") IsNot Nothing Then current_level = SESSION("access_level")
        Dim rule_level As Integer

        Dim rules As Hashtable = config("access_levels")
        If rules.ContainsKey(path) Then
            rule_level = rules(path)
        ElseIf rules.ContainsKey(path2) Then
            rule_level = rules(path2)
        Else
            rule_level = -1 'no restrictions
        End If

        If current_level >= rule_level Then result = True
        If Not result AndAlso is_die Then Throw New AuthException("Bad access - Not authorized")
        Return result
    End Function

    Private Sub parse_form()
        Dim f As Hashtable = New Hashtable

        Dim s As String
        For Each s In req.QueryString.Keys
            If s IsNot Nothing Then f(s) = req.QueryString(s)
        Next s

        For Each s In req.Form.Keys
            If s IsNot Nothing Then f(s) = req.Form(s)
        Next s

        'after perpare_FORM - grouping for names like XXX[YYYY] -> FORM{XXX}=@{YYYY1, YYYY2, ...}
        Dim SQ As Hashtable = New Hashtable
        Dim k As String
        Dim sk As String
        Dim v As String
        Dim rem_keys As ArrayList = New ArrayList

        For Each s In f.Keys
            Dim m As Match = Regex.Match(s, "^([^\]]+)\[([^\]]+)\]$")
            If m.Groups.Count > 1 Then
                k = m.Groups(1).ToString()
                sk = m.Groups(2).ToString()
                v = f(s)
                If Not SQ.ContainsKey(k) Then SQ(k) = New Hashtable
                SQ.Item(k).item(sk) = v
                rem_keys.Add(s)
            End If
        Next s

        For Each k In rem_keys
            f.Remove(k)
        Next

        For Each s In SQ.Keys
            f(s) = SQ(s)
        Next s
        f("RAWURL") = req.RawUrl
        'logger(f)
        FORM = f
    End Sub

    Public Overloads Sub logger(ByRef dmp_obj As Object)
        logger("DEBUG", dmp_obj)
    End Sub

    Public Overloads Sub logger(ByVal level As String, ByRef dmp_obj As Object)
        If Regex.IsMatch(level, "debug", RegexOptions.IgnoreCase) Then
            'skip logging if debug level and is_debug not set
            If Not Me.config().ContainsKey("is_debug") Then
                Return
            End If
            Dim is_d As Boolean = False
            Boolean.TryParse(Me.config("is_debug"), is_d)
            'skip logging if debug level and is_debug not True
            If Not is_d Then
                Return
            End If
        End If

        Dim str As String = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ff") & " " & level & " "
        Dim st As New Diagnostics.StackTrace(True)
        Dim sf As Diagnostics.StackFrame = st.GetFrame(1)

        Try
            If sf.GetMethod().Name = "logger" Then sf = st.GetFrame(2)
            str &= Replace(sf.GetFileName().ToString(), Me.config("site_root"), "") & ":" & sf.GetMethod().Name & " " & sf.GetFileLineNumber().ToString & " # "
        Catch ex As Exception
            str &= " ... #"
        End Try

        Try
            str &= dumper(dmp_obj)
        Catch ex As Exception
            str &= ex.ToString
        End Try

        'write to debug console first
        Diagnostics.Debug.WriteLine(str)

        'write to log file
        Dim log_file As String = config("log")
        Try
            'keep log file open to avoid overhead
            If floggerFS Is Nothing Then
                'open log with shared read/write so loggers from other processes can still write to it
                floggerFS = New FileStream(log_file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
                floggerSW = New System.IO.StreamWriter(floggerFS)
                floggerSW.AutoFlush = True
            End If
            'force seek to end just in case other process added to file
            floggerFS.Seek(0, SeekOrigin.End)
            floggerSW.WriteLine(str.ToString)
        Catch ex As Exception
            Diagnostics.Debug.WriteLine("WARN logger can't write to log file. Reason:" & ex.Message)
        End Try
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
                    Dim k As String
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
        Dim result As String() = {}
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

    'show page from template  /cur_controller/cur_action = parser('/cur_controller/cur_action/', $ps)
    Public Overloads Sub parser(hf As Hashtable)
        Me.parser(LCase(cur_controller_path & "/" & cur_action), hf)
    End Sub

    'same as parsert(hf), but with base dir param
    'output format based on requested format: json, pjax or (default) full page html
    'for automatic json response support - set hf("_json_enabled") = True - TODO make it better?
    'to return only specific content for json - set it to hf("_json_data")
    'to override page template - set hf("_page_tpl")="another_page_layout.html"
    '(not for json) to perform route_redirect - set hf("_route_redirect"), hf("_route_redirect_controller"), hf("_route_redirect_args")
    '(not for json) to perform redirect - set hf("_redirect")="url"
    'TODO - create another func and call it from call_controller for processing _redirect, ... (non-parsepage) instead of calling parser?
    Public Overloads Sub parser(ByVal bdir As String, hf As Hashtable)
        hf("ERR") = Me.FERR 'add errors if any

        Dim format As String = Me.get_response_expected_format()
        If format = "json" Then
            If hf("_json_enabled") = True Then
                If hf.ContainsKey("_json_data") Then
                    'if _json_data exists - return only this element
                    Me.parser_json(hf("_json_data"))
                Else
                    Me.parser_json(hf)
                End If
            Else
                Dim ps As New Hashtable
                ps("success") = False
                ps("message") = "JSON response is not enabled for this Controller.Action (set ps(""_json_enabled"")=True to enable)."
                Me.parser_json(ps)
            End If

            Return 'no further processing for json
        End If

        If hf.ContainsKey("_route_redirect") Then
            Me.route_redirect(hf("_route_redirect"), hf("_route_redirect_controller"), hf("_route_redirect_args"))
            Return 'no further processing
        End If

        If hf.ContainsKey("_redirect") Then
            Me.redirect(hf("_redirect"))
            Return 'no further processing
        End If

        Me.resp.CacheControl = "no-cache" 'disable cacheing of dynamic pages, TODO give controllers control over this
        If format = "pjax" Then
            Dim page_tpl As String = G("PAGE_TPL_PJAX")
            parser(bdir, page_tpl, hf)

        Else
            Dim page_tpl As String = G("PAGE_TPL")
            If hf.ContainsKey("_page_tpl") Then page_tpl = hf("_page_tpl")
            parser(bdir, page_tpl, hf)
        End If

    End Sub

    '- show page from template  /controller/action = parser('/controller/action/', $layout, $ps)
    Public Overloads Sub parser(ByVal bdir As String, ByVal tpl_name As String, ByVal hf As Hashtable)
        logger("parsing page bdir=" & bdir & ", tpl=" & tpl_name)
        Dim parser_obj As ParsePage = New ParsePage(Me)
        Dim page As String = parser_obj.parse_page(bdir, tpl_name, hf)
        resp.Write(page)
    End Sub

    Public Sub parser_json(ByVal hf As Object)
        Dim parser_obj As ParsePage = New ParsePage(Me)
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

    Public Overloads Sub route_redirect(ByVal action As String, ByVal controller As String, Optional ByVal args As Object = Nothing)
        cur_action = action
        If controller IsNot Nothing Then
            'TODO implement set_controller 
            cur_controller = controller
        End If

        G("controller") = cur_controller
        G("action") = cur_action
        G("controller.action") = cur_controller & "." & cur_action

        Dim calledType As Type = Type.GetType(cur_controller & "Controller", True)
        Dim mInfo As MethodInfo = calledType.GetMethod(cur_action & "Action")
        If IsNothing(mInfo) Then
            logger("WARN", "No method found for controller.action=[" & cur_controller & "." & cur_action & "], using default Index")
            'no method found - set to default Index method
            'cur_action = "Index"
            'mInfo = calledType.GetMethod(cur_action & "Action")

            'if no method - show template from /cur_controller/cur_action dir
            parser("/" & LCase(cur_controller) & "/" & LCase(cur_action), New Hashtable)
        End If

        If mInfo IsNot Nothing Then
            call_controller(calledType, mInfo, args)
        End If

    End Sub
    'same as above just with default controller
    Public Overloads Sub route_redirect(ByVal action As String, Optional ByVal args As Object = Nothing)
        route_redirect(action, cur_controller, args)
    End Sub

    'Call controller
    Public Sub call_controller(calledType As Type, mInfo As MethodInfo, Optional ByVal args As Object = Nothing)
        'check if method assept agrs and not pass it if no args expected
        Dim params() As System.Reflection.ParameterInfo = mInfo.GetParameters()
        If params.Length = 0 Then args = Nothing

        Dim new_controller As FwController = Activator.CreateInstance(calledType)
        new_controller.init(Me)
        Dim ps As Hashtable = Nothing
        Try
            ps = mInfo.Invoke(new_controller, args)
        Catch ex As TargetInvocationException
            Throw ex.InnerException
        End Try
        If ps IsNot Nothing Then parser(ps)
    End Sub


    Public Sub file_response(ByVal filepath As String, ByVal attname As String, Optional ContentType As String = "application/octet-stream", Optional ContentDisposition As String = "attachment")
        attname = Regex.Replace(attname, "[^\w. \-]+", "_")
        resp.AppendHeader("Content-type", ContentType)
        resp.AppendHeader("Content-Length", Utils.file_size(filepath))
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
    'RETURN:
    ' true if sent successfully
    ' false if some problem occured (see log)
    Public Function send_email(ByVal mail_from As String, ByVal mail_to As String, ByVal mail_subject As String, ByVal mail_body As String, Optional filenames As Hashtable = Nothing, Optional aCC As ArrayList = Nothing, Optional reply_to As String = "") As Boolean
        Dim result As Boolean = True
        Dim message As MailMessage = Nothing

        Try
            If Len(mail_from) = 0 Then mail_from = Me.config("mail_from") 'default mail from
            mail_subject = Regex.Replace(mail_subject, "[\r\n]+", " ")

            If Me.config("is_test") Then
                Dim test_email As String = Me.config("test_email")
                mail_body = "TEST SEND. PASSED MAIL_TO=[" & mail_to & "]" & vbCrLf & mail_body
                mail_to = Me.config("test_email")
                logger("DEBUG", "EMAIL SENT TO TEST EMAIL [" & mail_to & "] - TEST ENABLED IN web.config")
            End If

            logger("INFO", "Sending email. From=[" & mail_from & "], ReplyTo=[" & reply_to & "], To=[" & mail_to & "], Subj=[" & mail_subject & "]")
            logger("DEBUG", mail_body)

            If mail_to > "" Then

                message = New MailMessage

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
                Dim amail_to As ArrayList = Utils.email_split(mail_to)
                For Each email As String In amail_to
                    email = Trim(email)
                    If email = "" Then Continue For
                    message.To.Add(New MailAddress(email))
                Next

                'add CC if any
                If Not IsNothing(aCC) Then
                    If Me.config("is_test") Then
                        For Each cc As String In aCC
                            logger("DEBUG", "TEST SEND. PASSED CC=[" & cc & "]")
                            message.CC.Add(New MailAddress(mail_to))
                        Next
                    Else
                        For Each cc As String In aCC
                            cc = Trim(cc)
                            If cc = "" Then Continue For
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
                        Dim att As New Attachment(filename, Net.Mime.MediaTypeNames.Application.Octet)
                        'att.ContentDisposition.FileName = human_filename
                        att.Name = human_filename
                        att.NameEncoding = System.Text.Encoding.UTF8
                        logger("DEBUG", "attachment " & human_filename & " => " & filename)
                        message.Attachments.Add(att)
                    Next
                End If

                Dim client As SmtpClient = New SmtpClient()
                client.Send(message)
                'client.SendAsync(message,"") 'async alternative
            End If

        Catch ex As Exception
            result = False
            logger("ERROR", "send_email error")
            logger("ERROR", ex.Message)
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

    Public Function load_url(ByVal url As String) As String
        Dim client As System.Net.WebClient = New System.Net.WebClient
        Dim content As String = client.DownloadString(url)
        Return content
    End Function

    Public Sub err_msg(ByVal msg As String, Optional Ex As Exception = Nothing)
        Dim hf As Hashtable = New Hashtable

        hf("err_time") = Now()
        hf("err_msg") = msg
        If Utils.f2bool(Me.config("is_debug")) AndAlso Me.config("debug_level") = "SCREEN" Then
            hf("is_dump") = True
            If Ex IsNot Nothing Then
                hf("DUMP_STACK") = Ex.ToString()
            End If
            hf("DUMP_FORM") = dumper(FORM)
            hf("DUMP_SESSION") = dumper(SESSION)
        End If

        hf("success") = False
        hf("message") = msg
        hf("_json_enabled") = True
        parser("/error", hf)
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

    Public Sub log_event(ev_icode As String, Optional item_id As Integer = 0, Optional item_id2 As Integer = 0, Optional iname As String = "", Optional records_affected As Integer = 0)
        Me.model(Of Events).log_event(ev_icode, item_id, item_id2, iname, records_affected)
    End Sub

#Region "IDisposable Support"
    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                'dispose managed state (managed objects).
            End If

            'free unmanaged resources (unmanaged objects) and override Finalize() below.
            Try
                db.disconnect() 'this will return db connections to pool
                If floggerSW IsNot Nothing Then floggerSW.Close() 'no need to close floggerFS as StreamWriter closes it
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
