' WebFormMailer controller
' Send all form fields to site support_email
' Then redirect to form("redirect") url
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class WebFormMailerController
    Inherits FwController

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
    End Sub

    Public Sub SaveAction()
        Dim mail_from As String = fw.config("mail_from")
        Dim mail_to As String = fw.config("support_email")
        Dim mail_subject As String = reqs("subject")
        Dim redirect_to As String = reqs("redirect")

        Dim sys_fields As Hashtable = Utils.qh("form_format redirect subject submit RAWURL XSS")

        Dim msg_body As New StringBuilder
        For Each key As String In fw.FORM.Keys
            If sys_fields.ContainsKey(key) Then Continue For
            msg_body.AppendLine(key & " = " & fw.FORM(key))
        Next

        fw.send_email(mail_from, mail_to, mail_subject, msg_body.ToString())

        'need to add root_domain, so no one can use our redirector for bad purposes
        fw.redirect(fw.config("ROOT_DOMAIN") & redirect_to)
    End Sub

End Class

