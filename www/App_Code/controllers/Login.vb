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
    End Sub

    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable
        If fw.SESSION("is_logged") Then fw.redirect(fw.config("LOGGED_DEFAULT_URL"))

        Dim item As Hashtable = fw.FORM("item")
        If fw.cur_method = "GET" Then 'read from db
            'set defaults here
            item = New Hashtable
            'item("chpwd") = 1
        Else
            'read from form and make additional changes
        End If

        hf("login_mode") = fw.FORM("mode")
        hf("hide_sidebar") = True

        hf("i") = item
        hf("err_ctr") = Utils.f2int(fw.G("err_ctr")) + 1
        hf("ERR") = fw.FERR
        Return hf
    End Function

    Public Sub SaveAction()
        Try
            Dim login As String = Trim(fw.FORM("item")("login"))
            Dim pwd As String = fw.FORM("item")("pwdh")
            'if use field with masked chars - read masked field
            If fw.FORM("item")("chpwd") = "1" Then pwd = fw.FORM("item")("pwd")
            pwd = Left(Trim(pwd), 32)

            If login.Length = 0 Or pwd.Length = 0 Then
                fw.FERR("REGISTER") = True
                Throw New ApplicationException("")
            End If

            Dim where As Hashtable = New Hashtable
            where("email") = login
            where("pwd") = pwd
            Dim hU As Hashtable = db.row("users", where)

            If Not hU.ContainsKey("access_level") OrElse hU("status") <> "0" Then
                Throw New ApplicationException("User Authentication Error")
            End If

            model.do_login(hU("id"))

            fw.redirect(fw.config("LOGGED_DEFAULT_URL"))


        Catch ex As ApplicationException
            fw.G("err_ctr") = Utils.f2int(fw.FORM("err_ctr")) + 1
            fw.G("err_msg") = ex.Message
            fw.route_redirect("Index")
        End Try

    End Sub

    Public Sub DeleteAction()
        fw.SESSION.Clear()
        fw.SESSION.Abandon()
        fw.redirect(fw.config("UNLOGGED_DEFAULT_URL"))
    End Sub

End Class

