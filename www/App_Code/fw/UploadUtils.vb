' Upload and Image manipulation framework utils
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO

Public Class UploadParams
    Public fw As FW
    'input params:
    Public is_required As Boolean = False 'set to True and upload_simple will throw ApplicationException if file required, but not uploaded
    Public is_mkdir As Boolean = True 'create save_path if not exists
    Public is_overwrite As Boolean = True 'overwrite existing file
    Public is_cleanup As Boolean = False 'only if is_overwrite=true, apply remove_upload_img to destination path (cleans all old jpg/png/gif with thumbnails)
    Public is_resize As Boolean = False 'resize to max w/h if image
    Public max_w As Integer = 10000 ' default max image width
    Public max_h As Integer = 10000 ' default max iamge height

    Public field_name As String
    Public allowed_ext As Hashtable 'if empty - all exts allowed, exts should be with dots
    Public save_path As String
    Public save_filename As String 'without ext, ext will be same as upload file, if empty - use orig filename from upload field
    Public max_filesize As ULong = 0 'max allowed filesize, if 0 - allow all

    'output params:
    Public orig_filename As String 'original filename with ext
    Public full_path As String 'full path to saved file
    Public filename As String 'saved filename with ext
    Public ext As String 'saved ext
    Public filesize As String 'saved file size

    'example: Dim up As New UploadParams("file1", ".doc .pdf")
    Public Sub New(fw As FW, field_name As String, save_path As String, Optional save_filename_noext As String = "", Optional allowed_ext_str As String = "")
        Me.fw = fw
        Me.field_name = field_name
        Me.save_path = save_path
        Me.save_filename = save_filename_noext
        Me.allowed_ext = Utils.qh(allowed_ext_str)
    End Sub
End Class


Public Class UploadUtils
    'simple upload from posted field name to destination directory with different options
    Public Shared Function uploadSimple(up As UploadParams) As Boolean
        Dim result As Boolean = False

        Dim file As HttpPostedFile = up.fw.req.Files(up.field_name)
        If file IsNot Nothing Then
            up.orig_filename = file.FileName

            'check for allowed filesize 
            up.filesize = file.ContentLength
            If up.max_filesize > 0 AndAlso file.ContentLength > up.max_filesize Then
                If up.is_required Then Throw New ApplicationException("Uploaded file too large in size")
                Return result
            End If

            up.ext = UploadUtils.getUploadFileExt(file.FileName)
            'check for allowed ext
            If up.allowed_ext.Count > 0 AndAlso Not up.allowed_ext.ContainsKey(up.ext) Then
                If up.is_required Then Throw New ApplicationException("Uploaded file extension is not allowed")
                Return result
            End If

            'create target directory if required
            If up.is_mkdir AndAlso Not Directory.Exists(up.save_path) Then Directory.CreateDirectory(up.save_path)

            up.full_path = up.save_path
            If up.save_filename > "" Then
                up.filename = up.save_filename & up.ext
            Else
                up.filename = System.IO.Path.GetFileNameWithoutExtension(up.orig_filename) & up.ext
            End If
            up.full_path = up.full_path & "\" & up.filename

            If up.is_overwrite AndAlso up.is_cleanup Then removeUploadImgByPath(up.fw, up.full_path)

            If Not up.is_overwrite And System.IO.File.Exists(up.full_path) Then
                If up.is_required Then Throw New ApplicationException("Uploaded file cannot overwrite existing file")
                Return result
            End If

            up.fw.logger(LogLevel.DEBUG, "saving to ", up.full_path)
            file.SaveAs(up.full_path)

            If up.is_resize AndAlso isUploadImgExtAllowed(up.ext) Then
                Utils.resizeImage(up.full_path, up.full_path, up.max_w, up.max_h)
            End If
            result = True
        Else
            If up.is_required Then Throw New ApplicationException("No required file uploaded")
        End If

        Return result
    End Function

    'perform file upload for module_name/id and set filepath where it's stored, return true - if upload successful
    Public Overloads Shared Function uploadFile(fw As FW, ByVal module_name As String, id As Integer, ByRef filepath As String, Optional input_name As String = "file1", Optional is_skip_check As Boolean = False) As Boolean
        Dim result As Boolean = False
        Dim file As HttpPostedFile = fw.req.Files(input_name)

        filepath = uploadFileSave(fw, module_name, id, file, is_skip_check)

        Return result
    End Function

    'this one based on file index, not input name
    Public Overloads Shared Function uploadFile(fw As FW, ByVal module_name As String, id As Integer, ByRef filepath As String, Optional file_index As Integer = 0, Optional is_skip_check As Boolean = False) As Boolean
        Dim file As HttpPostedFile = fw.req.Files(file_index)

        filepath = uploadFileSave(fw, module_name, id, file, is_skip_check)

        Return (filepath > "")
    End Function

    Public Shared Function uploadFileSave(fw As FW, module_name As String, id As Integer, file As HttpPostedFile, Optional is_skip_check As Boolean = False) As String
        Dim result = ""
        If Not IsNothing(file) AndAlso file.ContentLength Then
            Dim ext As String = getUploadFileExt(file.FileName)
            If is_skip_check OrElse isUploadImgExtAllowed(ext) Then
                'remove any old files if necessary
                removeUploadImg(fw, module_name, id)

                'save original file
                Dim part As String = getUploadDir(fw, module_name, id) & "\" & id
                result = part & ext
                file.SaveAs(result)
            Else
                'Throw New ApplicationException("Image type is not supported")
            End If
        End If

        Return result
    End Function

    'return extension, lowercased, .jpeg=>.jpg
    'Usage: Dim ext As String = Utils.get_upload_file_ext(file.FileName) 'file As HttpPostedFile
    Public Shared Function getUploadFileExt(ByVal filename As String) As String
        Dim ext As String = System.IO.Path.GetExtension(filename).ToLower()
        If ext = ".jpeg" Then ext = ".jpg" 'small correction
        Return ext
    End Function

    'test if upload image extension is allowed
    Public Shared Function isUploadImgExtAllowed(ByVal ext As String) As Boolean
        If ext = ".jpg" Or ext = ".gif" Or ext = ".png" Then
            Return True
        Else
            Return False
        End If
    End Function

    'return upload dir for the module name and id related to FW.config("site_root")/upload
    ' id splitted to 1000
    Public Shared Function getUploadDir(fw As FW, ByVal module_name As String, ByVal id As Long) As String
        Dim dir As String = fw.config("site_root") & fw.config("UPLOAD_DIR") & "\" & module_name & "\" & (id Mod 1000)

        If Not Directory.Exists(dir) Then
            Directory.CreateDirectory(dir)
        End If

        Return dir
    End Function

    'similar to get_upload_dir, but return - DOESN'T check for file existance
    Public Shared Function getUploadUrl(fw As FW, ByVal module_name As String, ByVal id As Long, ByVal ext As String, Optional size As String = "") As String
        Dim url As String = fw.config("ROOT_URL") & "/upload/" & module_name & "/" & (id Mod 1000) & "/" & id
        If size > "" Then url &= "_" & size
        url &= ext

        Return url
    End Function

    'removes all type of image files uploaded with thumbnails
    Public Shared Function removeUploadImg(fw As FW, ByVal module_name As String, ByVal id As Long) As Boolean
        Dim dir As String = UploadUtils.getUploadDir(fw, module_name, id)
        Return removeUploadImgByPath(fw, dir & "\" & id)
    End Function

    Public Shared Function removeUploadImgByPath(fw As FW, path As String) As Boolean
        Dim dir As String = System.IO.Path.GetDirectoryName(path)
        path = dir & "\" & System.IO.Path.GetFileNameWithoutExtension(path) 'cut extension if any

        If Not Directory.Exists(dir) Then Return False

        File.Delete(path & "_l.png")
        File.Delete(path & "_l.gif")
        File.Delete(path & "_l.jpg")

        File.Delete(path & "_m.png")
        File.Delete(path & "_m.gif")
        File.Delete(path & "_m.jpg")

        File.Delete(path & "_s.png")
        File.Delete(path & "_s.gif")
        File.Delete(path & "_s.jpg")

        File.Delete(path & ".png")
        File.Delete(path & ".gif")
        File.Delete(path & ".jpg")
        Return True
    End Function

    'get correct image path for uploaded image
    ' size is one of: ""(original), "l", "m", "s"
    Public Shared Function getUploadImgPath(fw As FW, ByVal module_name As String, ByVal id As Long, ByVal size As String, Optional ext As String = "") As String
        If size <> "l" And size <> "m" And size <> "s" Then size = "" 'armor +1

        Dim part As String = UploadUtils.getUploadDir(fw, module_name, id) & "\" & id
        Dim orig_file As String = part

        If size > "" Then orig_file = orig_file & "_" & size

        If ext = "" Then
            If File.Exists(orig_file & ".gif") Then ext = ".gif"
            If File.Exists(orig_file & ".png") Then ext = ".png"
            If File.Exists(orig_file & ".jpg") Then ext = ".jpg"
        End If

        If ext > "" Then
            Return orig_file & ext
        Else
            Return ""
        End If
    End Function


    'get correct image URL for uploaded image
    Public Shared Function getUploadImgUrl(fw As FW, ByVal module_name As String, ByVal id As Long, ByVal size As String) As String
        If size <> "l" And size <> "m" And size <> "s" Then size = "" 'armor +1

        Dim part As String = UploadUtils.getUploadDir(fw, module_name, id) & "/" & id
        Dim orig_file As String = part

        If size > "" Then orig_file = orig_file & "_" & size

        Dim ext As String = ""
        If File.Exists(orig_file & ".gif") Then ext = ".gif"
        If File.Exists(orig_file & ".png") Then ext = ".png"
        If File.Exists(orig_file & ".jpg") Then ext = ".jpg"

        If ext > "" Then
            Return UploadUtils.getUploadUrl(fw, module_name, id, ext, size)
        Else
            Return ""
        End If
    End Function

End Class
