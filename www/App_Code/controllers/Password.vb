' Forgotten Password controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class PasswordController
    Inherits FwController
    Protected model As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        base_url = "/Password" 'base url for the controller
    End Sub


    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable

        Dim item As Hashtable = fw.FORM("item")
        If fw.cur_method = "GET" Then 'read from db
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
            Dim login As String = Trim(fw.FORM("item")("login"))

            If login.Length = 0 Then Throw New ApplicationException("Please enter your Email")

            Dim where As Hashtable = New Hashtable
            where("email") = login
            Dim hU As Hashtable = db.row("users", where)

            If Not hU.ContainsKey("id") OrElse hU("status") <> "0" Then
                Throw New ApplicationException("Not a valid Email")
            End If

            fw.send_email_tpl(hU("email"), "email_pwd.txt", hU)

            fw.redirect(base_url & "/(Sent)")
        Catch ex As Exception
            fw.G("err_msg") = ex.Message
            fw.route_redirect("Index")
        End Try

    End Sub

End Class

