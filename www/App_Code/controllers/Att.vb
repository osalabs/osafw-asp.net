' Att public downloads controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.Net
Imports System.IO

Public Class AttController
    Inherits FwController
    Protected model As New Att

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
    End Sub

    Public Sub IndexAction()
        fw.redirect(fw.config("ASSETS_URL") & "/img/0.gif")
        'Throw New ApplicationException("No file specified")
    End Sub

    Public Sub DownloadAction(Optional ByVal form_id As String = "")
        Dim id As Integer = Utils.f2int(form_id)
        If id = 0 Then Throw New ApplicationException("404 File Not Found")
        Dim size As String = reqs("size")

        Dim item As Hashtable = model.one(id)
        If item("is_s3") = "1" Then model.redirectS3(item, size)

        model.transmitFile(Utils.f2int(form_id), size)
    End Sub

    Public Sub ShowAction(Optional ByVal form_id As String = "")
        Dim id As Integer = Utils.f2int(form_id)
        If id = 0 Then Throw New ApplicationException("404 File Not Found")
        Dim size As String = reqs("size")
        Dim is_preview As Boolean = reqs("preview") = "1"

        Dim item As Hashtable = model.one(id)
        If item("is_s3") = "1" Then model.redirectS3(item, size)

        If is_preview Then

            If item("is_image") Then
                model.transmitFile(id, size, "inline")
            Else
                'if it's not an image and requested preview - return std image
                Dim filepath As String = fw.config("site_root") & "/img/att_file.png" ' TODO move to web.config or to model? and no need for transfer file - just redirect TODO
                Dim ext As String = UploadUtils.getUploadFileExt(filepath)
                fw.resp.AppendHeader("Content-type", model.getMimeForExt(ext))
                fw.resp.TransmitFile(filepath, "", "inline")
            End If
        Else
            model.transmitFile(id, size, "inline")
        End If

    End Sub

End Class

