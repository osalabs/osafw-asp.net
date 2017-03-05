' Signup controller (register new user)
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class SignupController
    Inherits FwController
    Protected model As New Users
    Public Shared Shadows route_default_action As String = "index"

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        required_fields = "email pwd"
        base_url = "/Signup"
    End Sub

    Public Sub IndexAction()
        fw.route_redirect("ShowForm")
    End Sub

    Public Function ShowFormAction() As Hashtable
        Dim hf As Hashtable = New Hashtable
        Dim item As New Hashtable

        If fw.cur_method = "GET" Then 'read from db
            'set defaults here
            'item("field")='default value'
        Else
            'and merge new values from the form
            Utils.hash_merge(item, reqh("item"))
            'here make additional changes if necessary
        End If

        hf("i") = item

        Return hf
    End Function

    Public Sub SaveAction(Optional ByVal form_id As String = "")
        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)

        Try
            Validate(item)
            'load old record if necessary
            'Dim itemdb As Hashtable = model.one(id)

            item = FormUtils.form2dbhash(item, Utils.qw("email pwd fname lname"))

            If id > 0 Then
                model.update(id, item)
                fw.FLASH("record_updated", 1)
            Else
                item("access_level") = 0
                item("add_user_id") = 0
                id = model.add(item)
                fw.FLASH("record_added", 1)
            End If

            model.do_login(id)
            fw.redirect(fw.config("LOGGED_DEFAULT_URL"))

        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            Dim args() As [String] = {id}
            fw.route_redirect("ShowForm", Nothing, args)
        End Try
    End Sub

    Public Function Validate(item As Hashtable) As Boolean
        Dim msg As String = ""
        Dim result As Boolean = True
        result = result And validate_required(item, Utils.qw(required_fields))
        If Not result Then msg = "Please fill in all required fields"

        If result AndAlso model.is_exists(item("email"), 0) Then
            result = False
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.is_email(item("email")) Then
            result = False
            fw.FERR("email") = "WRONG"
        End If

        If result AndAlso item("pwd") <> item("pwd2") Then
            result = False
            fw.FERR("pwd") = "WRONG"
        End If

        If Not result Then Throw New ApplicationException(msg)
        Return True
    End Function

End Class
