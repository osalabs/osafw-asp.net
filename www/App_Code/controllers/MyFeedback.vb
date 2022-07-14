' My Feedback controller
' when user post feedback - send it to the support_email
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2020 Oleg Savchuk www.osalabs.com

Public Class MyFeedbackController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 0

    Protected model As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        required_fields = "iname idesc" 'default required fields, space-separated
        base_url = "/My/Feedback" 'base url for the controller

        save_fields = "iname idesc"
    End Sub

    Public Sub IndexAction()
        Throw New ApplicationException("Not Implemented")
    End Sub


    Public Sub SaveAction()
        Dim item = reqh("item")
        Dim id = model.meId()

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim itemold As Hashtable = model.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, save_fields)
            Dim user = fw.model(Of Users).one(id)
            Dim ps As New Hashtable From {
                    {"user", user},
                    {"i", itemdb},
                    {"url", return_url}
                }
            fw.send_email_tpl(fw.config("support_email"), "feedback.txt", ps, Nothing, Nothing, user("email"))

            fw.FLASH("success", "Feedback sent. Thank you.")
        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            'if error - just ignore
        End Try

        fw.redirect(Me.getReturnLocation())
    End Sub

    Public Function Validate(id As String, item As Hashtable) As Boolean
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw(required_fields))
        If Not result Then fw.FERR("REQ") = 1

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
