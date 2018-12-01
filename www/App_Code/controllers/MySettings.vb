' My Settings controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class MySettingsController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 0

    Protected model As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        required_fields = "email" 'default required fields, space-separated
        base_url = "/My/Settings" 'base url for the controller
    End Sub

    Public Sub IndexAction()
        Dim args() As [String] = {}
        fw.route_redirect("ShowForm", Nothing, args)
    End Sub


    Public Function ShowFormAction() As Hashtable
        Dim hf As Hashtable = New Hashtable
        Dim item As Hashtable
        Dim id As Integer = fw.SESSION("user_id")

        If fw.cur_method = "GET" Then 'read from db
            item = model.one(id)
        Else
            'read from db
            item = model.one(id)
            'and merge new values from the form
            Utils.mergeHash(item, reqh("item"))
            'here make additional changes if necessary
        End If

        hf("id") = id
        hf("i") = item

        Return hf
    End Function

    Public Sub SaveAction()
        Dim item As Hashtable = reqh("item")
        Dim id As Integer = fw.SESSION("user_id")

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim itemold As Hashtable = model.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Utils.qw("email fname lname address1 address2 city state zip phone"))
            model.update(id, itemdb)
            fw.FLASH("record_updated", 1)

            If id = model.meId Then model.reloadSession()

            fw.redirect(base_url & "/" & id & "/edit")
        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            Dim args() As [String] = {id}
            fw.route_redirect("ShowForm", Nothing, args)
        End Try
    End Sub

    Public Function Validate(id As String, item As Hashtable) As Boolean
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw(required_fields))
        If Not result Then fw.FERR("REQ") = 1

        If result AndAlso model.isExists(item("email"), id) Then
            result = False
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.isEmail(item("email")) Then
            result = False
            fw.FERR("email") = "WRONG"
        End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    result = False
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        If fw.FERR.Count > 0 AndAlso Not fw.FERR.ContainsKey("REQ") Then
            fw.FERR("INVALID") = 1
        End If

        If Not result Then Throw New ApplicationException("")
        Return True
    End Function

End Class
