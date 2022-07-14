' Forgotten Password controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class PasswordController
    Inherits FwController
    Protected model As New Users

    Protected PWD_RESET_EXPIRATION As Integer = 60 'minutes

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        base_url = "/Password" 'base url for the controller
        'override layout
        fw.G("PAGE_LAYOUT") = fw.G("PAGE_LAYOUT_PUBLIC")
    End Sub


    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable

        Dim item As Hashtable = reqh("item")
        If isGet() Then 'read from db
            'set defaults here
            item = New Hashtable
        Else
            'read from form and make additional changes
        End If

        hf("i") = item
        hf("hide_sidebar") = True
        Return hf
    End Function

    Public Sub SaveAction()
        Try
            Dim login As String = Trim(reqh("item")("login"))

            If login.Length = 0 Then Throw New ApplicationException("Please enter your Email")

            Dim hU As Hashtable = model.oneByEmail(login)
            If Not hU.ContainsKey("id") OrElse hU("status") <> "0" Then Throw New ApplicationException("Not a valid Email")

            model.sendPwdReset(hU("id"))

            fw.redirect(base_url & "/(Sent)")
        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            fw.routeRedirect("Index")
        End Try

    End Sub

    Public Function ResetAction() As Hashtable
        Dim ps As New Hashtable
        Dim login = reqs("login")
        Dim token = reqs("token")
        Dim hU = model.oneByEmail(login)
        If Not hU.ContainsKey("id") OrElse hU("status") <> "0" Then Throw New ApplicationException("Not a valid Email")

        If hU("pwd_reset") = "" OrElse Not model.checkPwd(token, hU("pwd_reset")) OrElse DateDiff(DateInterval.Minute, Utils.f2date(hU("pwd_reset_time")), Now()) > PWD_RESET_EXPIRATION Then
            fw.FLASH("error", "Password reset token expired. Use Forgotten password link again.")
            fw.redirect("/Login")
        End If

        Dim item = reqh("item")
        If isGet() Then 'read from db
            'set defaults here
            item = New Hashtable
        Else
            'read from form and make additional changes
        End If

        ps("user") = hU
        ps("token") = token
        ps("i") = item
        ps("hide_sidebar") = True

        Return ps
    End Function


    Public Sub SaveResetAction()
        Dim item = reqh("item")
        Dim login = reqs("login")
        Dim token = reqs("token")
        Dim hU = model.oneByEmail(login)
        If Not hU.ContainsKey("id") OrElse hU("status") <> "0" Then Throw New ApplicationException("Not a valid Email")

        If hU("pwd_reset") = "" OrElse Not model.checkPwd(token, hU("pwd_reset")) OrElse DateDiff(DateInterval.Minute, Utils.f2date(hU("pwd_reset_time")), Now()) > PWD_RESET_EXPIRATION Then
            fw.FLASH("error", "Password reset token expired. Use Forgotten password link again.")
            fw.redirect("/Login")
        End If

        Dim id As Integer = hU("id")

        Try
            ValidateReset(id, item)
            'load old record if necessary
            'Dim itemdb As Hashtable = Users.one(id)

            Dim itemdb = FormUtils.filter(item, Utils.qw("pwd"))

            itemdb("pwd_reset") = "" 'also reset token
            model.update(id, itemdb)

            fw.logEvent("chpwd")
            fw.FLASH("success", "Password updated")

            fw.redirect("/Login")
        Catch ex As ApplicationException
            setFormError(ex)
            fw.routeRedirect("Reset")
        End Try
    End Sub

    Public Function ValidateReset(id As String, item As Hashtable) As Boolean
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw("pwd pwd2"))
        If Not result Then fw.FERR("REQ") = 1

        If result AndAlso item("pwd") <> item("pwd2") Then
            result = False
            fw.FERR("pwd2") = "NOTEQUAL"
        End If

        If fw.FERR.Count > 0 AndAlso Not fw.FERR.ContainsKey("REQ") Then
            fw.FERR("INVALID") = 1
        End If

        If Not result Then Throw New ApplicationException("")
        Return True
    End Function

    Public Function SentAction() As Hashtable
        Dim ps As New Hashtable
        ps("hide_sidebar") = True
        Return ps
    End Function


End Class

