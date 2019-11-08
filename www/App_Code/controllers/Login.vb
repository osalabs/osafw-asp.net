' Login and Registration Page controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class LoginController
    Inherits FwController
    Protected model As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        'override layout
        fw.G("PAGE_LAYOUT") = fw.G("PAGE_LAYOUT_PUBLIC")
    End Sub

    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable
        If fw.SESSION("is_logged") Then fw.redirect(fw.config("LOGGED_DEFAULT_URL"))

        Dim item As Hashtable = reqh("item")
        If fw.cur_method = "GET" Then 'read from db
            'set defaults here
            item = New Hashtable
            'item("chpwd") = 1
        Else
            'read from form and make additional changes
        End If

        hf("login_mode") = reqs("mode")
        hf("hide_sidebar") = True

        hf("i") = item
        hf("err_ctr") = Utils.f2int(fw.G("err_ctr")) + 1
        hf("ERR") = fw.FERR
        Return hf
    End Function

    Public Sub SaveAction()
        Try
            Dim item = reqh("item")
            Dim gourl = reqs("gourl")
            Dim login As String = Trim(item("login"))
            Dim pwd As String = item("pwdh")
            'if use field with masked chars - read masked field
            If item("chpwd") = "1" Then pwd = item("pwd")
            pwd = Trim(pwd)

            'for dev config only - login as first admin
            Dim is_dev_login = False
            If Utils.f2bool(fw.config("IS_DEV")) AndAlso login = "" AndAlso pwd = "~" Then
                Dim dev = db.row("select TOP 1 email, pwd from users where status=0 and access_level=100 order by id")
                login = dev("email")
                is_dev_login = True
            End If

            If login.Length = 0 Or pwd.Length = 0 Then
                fw.FERR("REGISTER") = True
                Throw New ApplicationException("")
            End If

            Dim hU = model.oneByEmail(login)
            If Not is_dev_login Then
                If hU.Count = 0 OrElse hU("status") <> "0" OrElse Not model.checkPwd(pwd, hU("pwd")) Then Throw New ApplicationException("User Authentication Error")
            End If

            model.doLogin(hU("id"))

            If gourl > "" AndAlso Not Regex.IsMatch(gourl, "^http", RegexOptions.IgnoreCase) Then 'if url set and not external url (hack!) given
                fw.redirect(gourl)
            Else
                fw.redirect(fw.config("LOGGED_DEFAULT_URL"))
            End If

        Catch ex As ApplicationException
            logger("ERROR", ex.Message)
            fw.G("err_ctr") = reqi("err_ctr") + 1
            fw.G("err_msg") = ex.Message
            fw.routeRedirect("Index")
        End Try

    End Sub

    Public Sub DeleteAction()
        fw.SESSION.Clear()
        fw.SESSION.Abandon()
        fw.redirect(fw.config("UNLOGGED_DEFAULT_URL"))
    End Sub

End Class

