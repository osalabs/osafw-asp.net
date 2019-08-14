' Att model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO

#Const is_S3 = False 'if you use Amazon.S3 set to True here and in S3 model

#If is_S3 Then
Imports Amazon
#End If

Public Class Att
    Inherits FwModel
    Public MIME_MAP As String = "doc|application/msword docx|application/msword xls|application/vnd.ms-excel xlsx|application/vnd.ms-excel ppt|application/vnd.ms-powerpoint pptx|application/vnd.ms-powerpoint pdf|application/pdf html|text/html zip|application/x-zip-compressed jpg|image/jpeg jpeg|image/jpeg gif|image/gif png|image/png wmv|video/x-ms-wmv avi|video/x-msvideo"
    Public att_table_link As String = "att_table_link"

    Public Sub New()
        MyBase.New()
        table_name = "att"
    End Sub

    'add/update att_table_links
    Public Sub updateAttLinks(table_name As String, id As Integer, form_att As Hashtable)
        If form_att Is Nothing Then Exit Sub

        Dim me_id As Integer = fw.model(Of Users).meId()

        '1. set status=1 (under update)
        Dim fields As New Hashtable
        fields("status") = 1
        Dim where As New Hashtable
        where("table_name") = table_name
        where("item_id") = id
        db.update(att_table_link, fields, where)

        '2. add new items or update old to status =0
        For Each form_att_id As String In form_att.Keys
            Dim att_id As Integer = Utils.f2int(form_att_id)
            If att_id = 0 Then Continue For

            where = New Hashtable
            where("table_name") = table_name
            where("item_id") = id
            where("att_id") = att_id
            Dim row As Hashtable = db.row(att_table_link, where)

            If row("id") > "" Then
                'existing link
                fields = New Hashtable
                fields("status") = 0
                where = New Hashtable
                where("id") = row("id")
                db.update(att_table_link, fields, where)
            Else
                'new link
                fields = New Hashtable
                fields("att_id") = att_id
                fields("table_name") = table_name
                fields("item_id") = id
                fields("add_users_id") = me_id
                db.insert(att_table_link, fields)
            End If
        Next

        '3. remove not updated atts (i.e. user removed them)
        where = New Hashtable
        where("table_name") = table_name
        where("item_id") = id
        where("status") = 1
        db.del(att_table_link, where)

    End Sub


    'return correct url
    Public Function getUrl(id As Integer, Optional size As String = "") As String
        'Dim item As Hashtable = one(id)
        'Return get_upload_url(id, item("ext"), size)
        If id = 0 Then Return ""

        'if /Att need to be on offline folder
        Dim result As String = fw.config("ROOT_URL") & "/Att/" & id
        If size > "" Then
            result &= "?size=" & size
        End If
        Return result
    End Function

    'return correct url - direct, i.e. not via /Att
    Public Overloads Function getUrlDirect(id As Integer, Optional size As String = "") As String
        Dim item As Hashtable = one(id)
        If item.Count = 0 Then Return ""

        Return getUrlDirect(item, size)
    End Function

    'if you already have item, must contain: item("id"), item("ext")
    Public Overloads Function getUrlDirect(item As Hashtable, Optional size As String = "") As String
        Return getUploadUrl(item("id"), item("ext"), size)
    End Function


    'IN: extension - doc, jpg, ... (dot is optional)
    'OUT: mime type or application/octetstream if not found
    Public Function getMimeForExt(ext As String) As String
        Dim result As String = ""
        Dim map As Hashtable = Utils.qh(MIME_MAP)
        ext = Regex.Replace(ext, "^\.", "") 'remove dot if any

        If map.ContainsKey(ext) Then
            result = map(ext)
        Else
            result = "application/octetstream"
        End If

        Return result
    End Function

    'mark record as deleted (status=127) OR actually delete from db (if is_perm)
    Public Overrides Sub delete(id As Integer, Optional is_perm As Boolean = False)
        'remove files first
        Dim item As Hashtable = one(id)
        If item("is_s3") = "1" Then
#If is_S3 Then
            'S3 storage - remove from S3
            fw.model(Of S3).deleteObject(table_name & "/" & item("id"))
#Else
            fw.logger(LogLevel.WARN, "Att record has S3 flag, but S3 storage is not enabled")
#End If
        Else
            'local storage
            Dim filepath As String = getUploadImgPath(id, "", item("ext"))
            If filepath > "" Then File.Delete(filepath)
            'for images - also delete s/m thumbnails
            If item("is_image") = 1 Then
                filepath = getUploadImgPath(id, "s", item("ext"))
                If filepath > "" Then File.Delete(filepath)
                filepath = getUploadImgPath(id, "m", item("ext"))
                If filepath > "" Then File.Delete(filepath)
            End If
        End If

        MyBase.delete(id, is_perm)
    End Sub

    'check access rights for current user for the file by id
    'generate exception
    Public Sub checkAccessRights(id As Integer)
        Dim result As Boolean = True
        Dim item As Hashtable = one(id)

        Dim user_access_level As Integer = fw.SESSION("access_level")

        'If item("access_level") > user_access_level Then
        '    result = False
        'End If

        'file must have Active status
        If item("status") <> 0 Then
            result = False
        End If

        If Not result Then Throw New ApplicationException("Access Denied. You don't have enough rights to get this file")
    End Sub

    'transimt file by id/size to user's browser, optional disposition - attachment(default)/inline
    'also check access rights - throws ApplicationException if file not accessible by cur user
    'if no file found - throws ApplicationException
    Public Sub transmitFile(id As Integer, Optional size As String = "", Optional disposition As String = "attachment")
        Dim item As Hashtable = one(id)
        If size <> "s" AndAlso size <> "m" Then size = ""

        If item("id") > 0 Then
            checkAccessRights(item("id"))
            fw.resp.Cache.SetCacheability(HttpCacheability.Private) 'use public only if all uploads are public
            fw.resp.Cache.SetExpires(DateTime.Now.AddDays(30)) 'cache for 30 days, this allows browser not to send any requests to server during this period (unless F5)
            fw.resp.Cache.SetMaxAge(New TimeSpan(30, 0, 0, 0))

            Dim filepath As String = getUploadImgPath(id, size, item("ext"))
            Dim filetime As Date = System.IO.File.GetLastWriteTime(filepath)
            filetime = New Date(filetime.Year, filetime.Month, filetime.Day, filetime.Hour, filetime.Minute, filetime.Second) 'remove any milliseconds

            fw.resp.Cache.SetLastModified(filetime) 'this allows browser to send If-Modified-Since request headers (unless Ctrl+F5)

            Dim ifmodhead As String = fw.req.Headers("If-Modified-Since")
            Dim ifmod As Date
            If ifmodhead IsNot Nothing AndAlso DateTime.TryParse(ifmodhead, ifmod) AndAlso ifmod.ToLocalTime >= filetime Then
                fw.resp.StatusCode = 304 'not modified
                fw.resp.SuppressContent = True
            Else
                fw.logger(LogLevel.INFO, "Transmit(", disposition, ") filepath [", filepath, "]")
                Dim filename As String = Replace(item("fname"), """", "'")
                Dim ext As String = UploadUtils.getUploadFileExt(filename)

                fw.resp.AppendHeader("Content-type", getMimeForExt(ext))
                fw.resp.AppendHeader("Content-Disposition", disposition & "; filename=""" & filename & """")

                fw.resp.TransmitFile(filepath)
            End If
        Else
            Throw New ApplicationException("No file specified")
        End If
    End Sub

    'return all att files linked via att_table_link
    ' is_image = -1 (all - files and images), 0 (files only), 1 (images only)
    Public Function getAllLinked(table_name As String, id As Integer, Optional is_image As Integer = -1) As ArrayList
        Dim where As String = ""
        If is_image > -1 Then
            where &= " and a.is_image=" & is_image
        End If
        Return db.array("select a.* " &
                    " from " & att_table_link & " atl, att a " &
                    " where atl.table_name=" & db.q(table_name) &
                    " and atl.item_id=" & db.qi(id) &
                    " and a.id=atl.att_id" &
                    where &
                    " order by a.id ")
    End Function


    'return first att image linked via att_table_link
    Public Function getFirstLinkedImage(table_name As String, id As Integer) As Hashtable
        Return db.row("select top 1 a.* " &
                    " from " & att_table_link & " atl, att a " &
                    " where atl.table_name=" & db.q(table_name) &
                    " and atl.item_id=" & db.qi(id) &
                    " and a.id=atl.att_id" &
                    " and a.is_image=1 " &
                    " order by a.id ")
    End Function

    'return all att images linked via att_table_link
    Public Function getAllLinkedImages(table_name As String, id As Integer) As ArrayList
        Return getAllLinked(table_name, id, 1)
    End Function

    'return all att files linked via att.table_name and att.item_id
    ' is_image = -1 (all - files and images), 0 (files only), 1 (images only)
    Public Function getAllByTableName(table_name As String, item_id As Integer, Optional is_image As Integer = -1) As ArrayList
        Dim where As String = ""
        If is_image > -1 Then
            where &= " and a.is_image=" & is_image
        End If
        Return db.array("select a.* " &
                    " from att a " &
                    " where a.table_name=" & db.q(table_name) &
                    " and a.item_id=" & db.qi(item_id) &
                    where &
                    " order by a.id ")
    End Function

    'return one att record with additional check by table_name
    Public Function oneByTableName(item_table_name As String, item_id As Integer) As Hashtable
        Return db.row(table_name, New Hashtable From {
                      {"table_name", item_table_name},
                      {"item_id", item_id}
                      })
    End Function

    'generate signed url and redirect to it, so user download directly from S3
    Public Sub redirectS3(item As Hashtable)
#If is_S3 Then
        If fw.model(Of Users).meId() = 0 Then Throw New ApplicationException("Access Denied") 'denied for non-logged

        Dim url = fw.model(Of S3).getSignedUrl(table_name & "/" & item("id"))

        fw.redirect(url)
#Else
        logger(LogLevel.WARN, "redirectS3 - S3 not enabled")
#End If
    End Sub

#If is_S3 Then

    ''' <summary>
    ''' upload all posted files (fw.req.Files) to S3 for the table
    ''' </summary>
    ''' <param name="item_table_name"></param>
    ''' <param name="item_id"></param>
    ''' <param name="att_categories_id"></param>
    ''' <param name="fieldnames">qw string of ONLY field names to upload</param>
    ''' <returns>number of successuflly uploaded files</returns>
    ''' <remarks>also set FLASH error if some files not uploaded</remarks>
    Public Function uploadPostedFilesS3(item_table_name As String, item_id As Integer, Optional att_categories_id As String = Nothing, Optional fieldnames As String = "") As Integer
        Dim result = 0

        'upload files to the S3
        Dim model_s3 = fw.model(Of S3)

        'create /att folder
        model_s3.createFolder(Me.table_name)

        Dim honlynames = Utils.qh(fieldnames)

        'create list of eligible file uploads
        Dim afiles As New ArrayList
        If honlynames.Count > 0 Then
            'if we only need some fields - skip if not requested field
            For Each fieldname In fw.req.Files.Keys
                If Not honlynames.ContainsKey(fieldname) Then Continue For
                afiles.Add(fw.req.Files(fieldname))
            Next
        Else
            'just add all files
            For i = 0 To fw.req.Files.Count - 1
                afiles.Add(fw.req.Files(i))
            Next
        End If

        'upload files to S3
        For Each file In afiles
            If file.ContentLength = 0 Then Continue For 'skip empty

            'first - save to db so we can get att_id
            Dim attitem As New Hashtable
            attitem("att_categories_id") = att_categories_id
            attitem("table_name") = item_table_name
            attitem("item_id") = item_id
            attitem("is_s3") = 1
            attitem("status") = 1
            attitem("fname") = file.FileName
            attitem("fsize") = file.ContentLength
            attitem("ext") = UploadUtils.getUploadFileExt(file.FileName)
            Dim att_id = fw.model(Of Att).add(attitem)

            Try
                Dim response = model_s3.uploadPostedFile(Me.table_name & "/" & att_id, file, "inline")

                'TODO check response for 200 and if not - error/delete?
                'once uploaded - mark in db as uploaded
                fw.model(Of Att).update(att_id, New Hashtable From {{"status", 0}})

                result += 1

            Catch ex As Amazon.S3.AmazonS3Exception
                logger(ex.Message)
                logger(ex)
                fw.FLASH("error", "Some files were not uploaded due to error. Please re-try.")
                'TODO if error - don't set status to 0 but remove att record?
                fw.model(Of Att).delete(att_id, True)
            End Try
        Next

        Return result
    End Function
#End If

End Class
