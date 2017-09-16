' MyPassword controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class MyPasswordController
    Inherits FwController
    Protected model As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
    End Sub

    Public Sub IndexAction()
        Dim args() As [String] = {}
        fw.route_redirect("ShowForm", "MyPassword", args)
    End Sub

    Public Function ShowFormAction() As Hashtable
        If reqs("result") = "record_updated" Then
            fw.G("green_msg") = "Login/Password has been changed"
        End If

        Dim hf As Hashtable = New Hashtable
        Dim item As Hashtable
        Dim id As Integer = fw.SESSION("user")("id")

        If fw.cur_method = "GET" Then 'read from db
            If id > 0 Then
                item = model.one(id)
            Else
                'set defaults here
                item = New Hashtable
                'item("field")='default value'
            End If
        Else
            'read from db
            item = model.one(id)
            'and merge new values from the form
            Utils.hash_merge(item, reqh("item"))
            'here make additional changes if necessary
        End If

        hf("id") = id
        hf("i") = item
        hf("ERR") = fw.FERR
        Return hf
    End Function

    Public Sub SaveAction()
        Dim item As New Hashtable
        Dim id As Integer = fw.SESSION("user")("id")

        Try
            Validate(id, reqh("item"))
            'load old record if necessary
            'Dim itemdb As Hashtable = Users.one(id)

            item = FormUtils.form2dbhash(reqh("item"), Utils.qw("email pwd"))

            If id > 0 Then
                'update
                item("upd_time") = Now()
                item("add_users_id") = fw.SESSION("user")("id")

                Dim where As New Hashtable
                where("id") = id
                db.update("users", item, where)

                fw.log_event("chpwd")
                fw.FLASH("record_updated", 1)
            End If

            fw.redirect("/My/Password/" & id & "/edit")
        Catch ex As ApplicationException
            fw.route_redirect("ShowForm")
        End Try
    End Sub

    Public Function Validate(id As String, item As Hashtable) As Boolean
        Dim result As Boolean = True
        result = result And validate_required(item, Utils.qw("email old_pwd pwd pwd2"))
        If Not result Then fw.FERR("REQ") = 1

        If result AndAlso model.is_exists(item("email"), id) Then
            result = False
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.is_email(item("email")) Then
            result = False
            fw.FERR("email") = "WRONG"
        End If

        If result AndAlso item("pwd") <> item("pwd2") Then
            result = False
            fw.FERR("pwd2") = "NOTEQUAL"
        End If

        If result Then
            Dim itemdb As Hashtable = model.one(id)
            If itemdb("pwd") <> item("old_pwd") Then
                result = False
                fw.FERR("old_pwd") = "WRONG"
            End If
        End If

        If fw.FERR.Count > 0 AndAlso Not fw.FERR.ContainsKey("REQ") Then
            fw.FERR("INVALID") = 1
        End If

        If Not result Then Throw New ApplicationException("")
        Return True
    End Function

End Class
