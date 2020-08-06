' MyPassword controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class MyPasswordController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 0

    Protected model As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
    End Sub

    Public Sub IndexAction()
        Dim args() As [String] = {}
        fw.routeRedirect("ShowForm", "MyPassword", args)
    End Sub

    Public Function ShowFormAction() As Hashtable
        If reqs("result") = "record_updated" Then
            fw.G("green_msg") = "Login/Password has been changed"
        End If

        Dim ps As New Hashtable
        Dim item As Hashtable
        Dim id As Integer = fw.model(Of Users).meId()

        If isGet() Then 'read from db
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
            Utils.mergeHash(item, reqh("item"))
            'here make additional changes if necessary
        End If

        ps("id") = id
        ps("i") = item
        ps("ERR") = fw.FERR
        Return ps
    End Function

    Public Sub SaveAction()
        Dim id As Integer = fw.model(Of Users).meId()

        Try
            Validate(id, reqh("item"))
            'load old record if necessary
            'Dim itemdb As Hashtable = Users.one(id)

            Dim itemdb = FormUtils.filter(reqh("item"), Utils.qw("email pwd"))
            itemdb("pwd") = Trim(itemdb("pwd"))

            If id > 0 Then
                model.update(id, itemdb)

                fw.logEvent("chpwd")
                fw.FLASH("record_updated", 1)
            End If

            fw.redirect("/My/Password/" & id & "/edit")
        Catch ex As ApplicationException
            fw.routeRedirect("ShowForm")
        End Try
    End Sub

    Public Function Validate(id As String, item As Hashtable) As Boolean
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw("email old_pwd pwd pwd2"))
        If Not result Then fw.FERR("REQ") = 1

        If result AndAlso model.isExists(item("email"), id) Then
            result = False
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.isEmail(item("email")) Then
            result = False
            fw.FERR("email") = "WRONG"
        End If

        If result AndAlso model.cleanPwd(item("pwd")) <> model.cleanPwd(item("pwd2")) Then
            result = False
            fw.FERR("pwd2") = "NOTEQUAL"
        End If

        'uncomment if project requires good password strength
        'If result AndAlso item.ContainsKey("pwd") AndAlso model.scorePwd(item("pwd")) <= 60 Then
        '    result = False
        '    fw.FERR("pwd") = "BAD"
        'End If

        If result Then
            Dim itemdb As Hashtable = model.one(id)
            If Not fw.model(Of Users).checkPwd(item("old_pwd"), itemdb("pwd")) Then
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
