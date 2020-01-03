' Fw Self Test base class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Imports System.IO

Public Class FwSelfTest
    Protected fw As FW
    Protected db As DB
    Public is_logged As Boolean = False
    Public is_db As Boolean = False 'set to true after db connection test if db connection successful

    Public ok_ctr As Integer = 0    'number of successfull tests
    Public warn_ctr As Integer = 0  'number of warning tests
    Public err_ctr As Integer = 0   'number of errorneous tests
    Public total_ctr As Integer = 0 'total tests

    'Test results for self-test
    Public Enum Result As Integer
        OK
        WARN
        ERR
    End Enum

    Public test_email As String = "" 'if empty, will use "test"+mail_from
    Public existing_tables As String = "users settings spages att att_table_link att_categories events event_log lookup_manager_tables user_views user_lists user_lists_items" 'check if these tables exists
    Public exclude_controllers As String = ""

    Public Sub New(fw As FW)
        Me.fw = fw
        Me.db = fw.db

        is_logged = Utils.f2bool(fw.SESSION("is_logged"))
    End Sub

    ''''''''''''''''''''''''''' high level tests

    ''' <summary>
    ''' run all tests
    ''' </summary>
    Public Overridable Sub all()
        configuration()
        database_tables()
        controllers()
    End Sub

    ''' <summary>
    ''' test config values. Override to make additional config tests
    ''' </summary>
    Public Overridable Sub configuration()
        'test important config settings
        echo("<strong>Config</strong>")
        echo("hostname: " & fw.config("hostname"))
        is_notempty("site_root", fw.config("site_root"))

        'log_level: higher than debug - OK, debug - warn, trace or below - red (not for prod)
        Dim log_level = CType(fw.config("log_level"), LogLevel)
        If log_level >= LogLevel.TRACE Then
            plus_err()
            echo("log_level", [Enum].GetName(GetType(LogLevel), log_level), Result.ERR)
        ElseIf log_level = LogLevel.DEBUG Then
            plus_warn()
            echo("log_level", [Enum].GetName(GetType(LogLevel), log_level), Result.WARN)
        Else
            plus_ok()
            echo("log_level", "OK")
        End If

        is_false("is_test", fw.config("is_test"), "Turned ON")

        'template directory should exists - TODO test parser to actually see templates work?
        is_true("template", Directory.Exists(fw.config("template")), fw.config("template"))
        is_true("access_levels", fw.config("access_levels") IsNot Nothing AndAlso fw.config("access_levels").Count > 0, "Not defined")

        'UPLOAD_DIR upload dir is writeable
        Try
            Dim upload_filepath As String = UploadUtils.getUploadDir(fw, "selftest", 1) & "/txt"
            FW.set_file_content(upload_filepath, "test")
            File.Delete(upload_filepath)
            plus_ok()
            echo("upload dir", "OK")
        Catch ex As Exception
            plus_err()
            echo("upload dir", ex.Message(), Result.ERR)
        End Try

        'emails set
        is_notempty("mail_from", fw.config("mail_from"))
        is_notempty("support_email", fw.config("support_email"))

        'test send email to "test+mail_from"
        is_true("Send Emails", fw.send_email("", IIf(String.IsNullOrEmpty(test_email), "test+" & fw.config("mail_from"), test_email), "test email", "test body"), "Failed")

        Try
            db.connect()
            is_db = True
            plus_ok()
            echo("DB connection", "OK")
        Catch ex As Exception
            plus_err()
            echo("DB connection", ex.Message(), Result.ERR)
        End Try
    End Sub

    Public Overridable Sub database_tables()
        If is_db Then
            echo("<strong>DB Tables</strong>")
            'fw core db tables exists and we can read from it 
            '(select count(*) from: users, settings, spages, att, att_table_link, att_categories, events, event_log)
            Dim tables As String() = Utils.qw(existing_tables)
            For Each table In tables
                Try
                    db.value("select TOP 1 * from " & table)
                    plus_ok()
                    echo("table " & table, "OK")
                Catch ex As Exception
                    plus_err()
                    echo("table " & table, ex.Message(), Result.ERR)
                End Try
            Next
        End If
    End Sub

    Public Overridable Sub controllers()
        'test controllers (TODO - only for logged admin user)
        echo("<strong>Controllers</strong>")

        'get all classes ending with "Controller" and not starting with "Fw"
        Dim aControllers = Reflection.Assembly.GetExecutingAssembly().GetTypes() _
            .Where(Function(t) t.Name <> "AdminSelfTestController" AndAlso Right(t.Name, 10) = "Controller" AndAlso Left(t.Name, 2) <> "Fw") _
            .OrderBy(Function(t) t.Name) _
            .ToList()

        If aControllers.Count = 0 Then
            plus_err()
            echo("Controllers", "None found", FwSelfTest.Result.ERR)
        End If

        Dim hexclude As Hashtable = Utils.qh(exclude_controllers)

        For Each t In aControllers
            Dim controller_name As String = Replace(t.Name, "Controller", "")
            'omit controllers we don't need to test
            If hexclude.ContainsKey(controller_name) Then Continue For

            fw.logger("Testing Controller:" & controller_name)

            Dim calledType As Type = Type.GetType(t.Name, False, True)
            If calledType Is Nothing Then
                plus_err()
                echo(t.Name, "Not found", FwSelfTest.Result.ERR)
                Continue For
            End If

            Try

                'check controller have SelfTest method
                'SelfTest method should accept one argument FwSelfTest 
                'and return FwSelfTest.Result
                'sample Controller.SelfTest declaration:
                '
                ' Public Function SelfTest(t As FwSelfTest) As FwSelfTest.Result
                '    Dim res As Boolean = True
                '    res = res AndAlso t.is_true("Inner var check", (var = 1)) = FwSelfTest.Result.OK
                '    Return IIf(res, FwSelfTest.Result.OK, FwSelfTest.Result.ERR)
                ' End Function

                Dim mInfo As Reflection.MethodInfo = calledType.GetMethod("SelfTest")
                If mInfo Is Nothing Then

                    'if no SelfTest - test IndexAction method
                    mInfo = calledType.GetMethod("IndexAction")
                    If mInfo Is Nothing Then
                        plus_warn()
                        echo(t.Name, "No SelfTest or IndexAction methods found", FwSelfTest.Result.WARN)
                        Continue For
                    End If

                    'test using IndexAction
                    'need to buffer output from controller to clear it later
                    fw.resp.BufferOutput = True

                    fw._auth(controller_name, "Index")
                    fw.setController(controller_name, "Index")

                    Dim new_controller As FwController = Activator.CreateInstance(calledType)
                    new_controller.init(fw)
                    Dim ps As Hashtable = mInfo.Invoke(new_controller, Nothing)
                    fw.resp.Clear()
                    fw.resp.BufferOutput = False

                    If ps Is Nothing OrElse ps.Count = 0 Then
                        plus_warn()
                        echo(t.Name, "Empty result", FwSelfTest.Result.WARN)
                    Else
                        plus_ok()
                        echo(t.Name, "OK")
                    End If

                Else
                    'test using SelfTest
                    fw._auth(controller_name, "SelfTest")

                    Dim new_controller As FwController = Activator.CreateInstance(calledType)
                    new_controller.init(fw)
                    Dim res As Result = mInfo.Invoke(new_controller, New Object() {Me})
                    If res = Result.OK Then
                        plus_ok()
                        echo(t.Name, "OK")
                    ElseIf res = Result.WARN Then
                        plus_warn()
                        echo(t.Name, "Warning", res)
                    ElseIf res = Result.ERR Then
                        plus_err()
                        echo(t.Name, "Error", res)
                    End If
                End If

            Catch ex As AuthException
                'just skip controllers not authorized to current user
                fw.logger(controller_name & " controller test skipped, user no authorized")

            Catch ex As Exception
                If fw.resp.BufferOutput Then
                    fw.resp.Clear()
                    fw.resp.BufferOutput = False
                End If

                If ex.InnerException IsNot Nothing Then
                    If TypeOf (ex.InnerException) Is RedirectException OrElse ex.InnerException.Message.Contains("Cannot redirect after HTTP headers have been sent.") Then
                        'just redirect in Controller.Index - it's OK
                        plus_ok()
                        echo(t.Name, "OK")
                    Else
                        'something really wrong
                        fw.logger(ex.InnerException.ToString())
                        plus_err()
                        echo(t.Name, ex.InnerException.Message(), FwSelfTest.Result.ERR)
                    End If
                ElseIf TypeOf (ex) Is RedirectException OrElse ex.Message.Contains("Cannot redirect after HTTP headers have been sent.") Then
                    'just redirect in Controller.Index - it's OK
                    plus_ok()
                    echo(t.Name, "OK")

                Else
                    'something really wrong
                    fw.logger(ex.ToString())
                    plus_err()
                    echo(t.Name, ex.Message(), FwSelfTest.Result.ERR)
                End If
            End Try

        Next

    End Sub


    ''' <summary>
    ''' default stub to output test header
    ''' </summary>
    Public Overridable Sub echo_start()
        echo("<h1>Site Self Test</h1>")
        'If Not is_logged Then echo("<a href='" & fw.config("ROOT_URL") & "/Login'>Login</a> as an administrator to see error details and perform additional tests")
        echo("<a href='#summary'>Test Summary</a>")
    End Sub

    ''' <summary>
    ''' ouput test totals, success, warnings, errors
    ''' </summary>
    Public Overridable Sub echo_totals()
        echo("<a name='summary' />")
        echo("<h2>Test Summary</h2>")
        echo("Total : " & total_ctr)
        echo("Success", ok_ctr)
        echo("Warnings", warn_ctr, Result.WARN)
        echo("Errors", err_ctr, Result.ERR)

        'self check
        If total_ctr <> ok_ctr + warn_ctr + err_ctr Then
            echo("Test count error", "total != ok+warn+err", Result.ERR)
        End If

        echo("<br><br><br><br><br>") 'add some footer spacing for easier review
    End Sub


    ''''''''''''''''''''''''''' low level tests

    ''' <summary>
    ''' test of value is false and ouput OK. If true output ERROR or custom string
    ''' </summary>
    ''' <param name="label"></param>
    ''' <param name="value"></param>
    ''' <param name="err_str"></param>
    Public Function is_false(label As String, value As Boolean, Optional err_str As String = "ERROR") As Result
        Dim res As Result = Result.ERR
        total_ctr += 1
        If value Then
            err_ctr += 1
        Else
            ok_ctr += 1
            err_str = "OK"
            res = Result.OK
        End If
        echo(label, err_str, res)
        Return res
    End Function

    ''' <summary>
    ''' test of value is true and ouput OK. If false output ERROR or custom string
    ''' </summary>
    ''' <param name="label"></param>
    ''' <param name="value"></param>
    ''' <param name="err_str"></param>
    Public Function is_true(label As String, value As Boolean, Optional err_str As String = "ERROR") As Result
        Dim res As Result = Result.ERR
        total_ctr += 1
        If value Then
            ok_ctr += 1
            err_str = "OK"
            res = Result.OK
        Else
            err_ctr += 1
        End If
        echo(label, err_str, res)
        Return res
    End Function

    ''' <summary>
    ''' test of value is not nothing and not empty string and ouput OK. If value is empty output ERROR or custom string
    ''' </summary>
    ''' <param name="label"></param>
    ''' <param name="value"></param>
    Public Function is_notempty(label As String, value As Object, Optional err_str As String = "EMPTY") As Result
        Dim res As Result = Result.ERR
        total_ctr += 1
        If String.IsNullOrEmpty(value) Then
            err_ctr += 1
        Else
            ok_ctr += 1
            err_str = "OK"
            res = Result.OK
        End If
        echo(label, err_str, res)
        Return res
    End Function

    ''' <summary>
    ''' test of value is nothing or empty string and ouput OK. If false output ERROR or custom string
    ''' </summary>
    ''' <param name="label"></param>
    ''' <param name="value"></param>
    Public Function is_empty(label As String, value As Object, Optional err_str As String = "EMPTY") As Result
        Dim res As Result = Result.ERR
        total_ctr += 1
        If String.IsNullOrEmpty(value) Then
            ok_ctr += 1
            err_str = "OK"
            res = Result.OK
        Else
            err_ctr += 1
        End If
        echo(label, err_str, res)
        Return res
    End Function

    ''' <summary>
    ''' output test result to browser, optionally with color.
    ''' </summary>
    ''' <param name="label">string to use as label</param>
    ''' <param name="str">test result value or just OK, ERROR</param>
    ''' <param name="res">result category</param>
    Public Sub echo(label As String, Optional str As String = "", Optional res As Result = Result.OK)
        If res = Result.WARN Then
            str = "<span style='background-color:#FF7200;color:#fff'>" & str & "</span>"
        ElseIf res = Result.ERR Then
            str = "<span style='background-color:#DD0000;color:#fff'>" & str & "</span>"
        Else
            If str > "" Then
                str = "<span style='color:#009900;'>" & str & "</span>"
            End If
        End If

        'Output without parser because templates might not exists/configured
        If str > "" Then
            fw.resp.Write(label & " : " & str)
        Else
            fw.resp.Write(label)
        End If

        fw.resp.Write("<br>" & vbCrLf)
        fw.resp.Flush()
    End Sub

    ''' <summary>
    ''' helper to add 1 to OK count
    ''' </summary>
    Public Sub plus_ok()
        ok_ctr += 1
        total_ctr += 1
    End Sub
    ''' <summary>
    ''' helper to add 1 to WARN count
    ''' </summary>
    Public Sub plus_warn()
        warn_ctr += 1
        total_ctr += 1
    End Sub
    ''' <summary>
    ''' helper to add 1 to ERR count
    ''' </summary>
    Public Sub plus_err()
        err_ctr += 1
        total_ctr += 1
    End Sub


End Class