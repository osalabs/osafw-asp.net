' Contact Us public controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2019 Oleg Savchuk www.osalabs.com

Imports System.Net
Imports System.IO

Public Class ContactController
    Inherits FwController

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        base_url = "/Contact"
        'override layout
        fw.G("PAGE_LAYOUT") = fw.G("PAGE_LAYOUT_PUBLIC")
    End Sub

    Public Function IndexAction() As Hashtable
        Dim ps As Hashtable = New Hashtable

        fw.SESSION("contact_view_time", Now())

        Dim page As Hashtable = fw.model(Of Spages).oneByFullUrl(base_url)
        ps("page") = page
        ps("hide_sidebar") = True
        Return ps
    End Function

    Public Sub SaveAction()
        Dim mail_from As String = fw.config("mail_from")
        Dim mail_to As String = fw.config("support_email")
        Dim mail_subject As String = "Contact Form Submission"

        'validation
        Dim is_spam = False
        Dim view_time = Utils.f2date(fw.SESSION("contact_view_time"))
        If view_time Is Nothing OrElse DateDiff(DateInterval.Second, view_time, Now()) < 5 Then
            is_spam = True
        End If
        If reqs("real_email") > "" Then
            'honeypot
            is_spam = True
        End If

        Dim sys_fields As Hashtable = Utils.qh("form_format redirect subject submit RAWURL XSS real_email")

        Dim msg_body As New StringBuilder
        For Each key As String In fw.FORM.Keys
            If sys_fields.ContainsKey(key) Then Continue For
            msg_body.AppendLine(key & " = " & fw.FORM(key))
        Next

        'ip address
        msg_body.AppendLine(vbCrLf & vbCrLf)
        Dim ip = fw.req.ServerVariables("HTTP_X_FORWARDED_FOR")
        If String.IsNullOrEmpty(ip) Then ip = fw.req.ServerVariables("REMOTE_ADDR")
        msg_body.AppendLine("IP: " & ip)

        If is_spam Then
            logger("* SPAM DETECTED: " & msg_body.ToString())
        Else
            fw.send_email(mail_from, mail_to, mail_subject, msg_body.ToString())
        End If

        'need to add root_domain, so no one can use our redirector for bad purposes
        fw.redirect(base_url & "/(Sent)")
    End Sub

    Public Function SentAction(Optional url As String = "") As Hashtable
        Dim ps As Hashtable = New Hashtable

        Dim page As Hashtable = fw.model(Of Spages).oneByFullUrl(base_url & "/Sent")
        ps("page") = page
        ps("hide_sidebar") = True
        Return ps
    End Function

End Class

