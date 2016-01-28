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
        Throw New ApplicationException("No file specified")
    End Sub

    Public Sub DownloadAction(Optional ByVal form_id As String = "")
        Dim id As Integer = Utils.f2int(form_id)
        If id = 0 Then Throw New ApplicationException("404 File Not Found")
        Dim size As String = fw.FORM("size")
        model.transmit_file(Utils.f2int(form_id), size)
    End Sub

    Public Sub ShowAction(Optional ByVal form_id As String = "")
        Dim id As Integer = Utils.f2int(form_id)
        If id = 0 Then Throw New ApplicationException("404 File Not Found")
        Dim size As String = fw.FORM("size")
        Dim is_preview As Boolean = fw.FORM("preview") = "1"

        If is_preview Then
            Dim item As Hashtable = model.one(id)
            If item("is_image") Then
                model.transmit_file(id, size, "inline")
            Else
                'if it's not an image and requested preview - return std image
                Dim filepath As String = fw.config("site_root") & "/img/att_file.png" ' TODO move to web.config or to model?
                Dim ext As String = UploadUtils.get_upload_file_ext(filepath)
                fw.resp.AppendHeader("Content-type", model.get_mime4ext(ext))
                fw.resp.TransmitFile(filepath)
            End If
        Else
            model.transmit_file(id, size, "inline")
        End If

    End Sub

End Class

