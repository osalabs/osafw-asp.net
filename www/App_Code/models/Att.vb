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
    Const MAX_THUMB_W_S As Integer = 180
    Const MAX_THUMB_H_S As Integer = 180
    Const MAX_THUMB_W_M As Integer = 512
    Const MAX_THUMB_H_M As Integer = 512
    Const MAX_THUMB_W_L As Integer = 1200
    Const MAX_THUMB_H_L As Integer = 1200

    Public MIME_MAP As String = "doc|application/msword docx|application/msword xls|application/vnd.ms-excel xlsx|application/vnd.ms-excel ppt|application/vnd.ms-powerpoint pptx|application/vnd.ms-powerpoint pdf|application/pdf html|text/html zip|application/x-zip-compressed jpg|image/jpeg jpeg|image/jpeg gif|image/gif png|image/png wmv|video/x-ms-wmv avi|video/x-msvideo mp4|video/mp4"
    Public att_table_link As String = "att_table_link"

    Public Sub New()
        MyBase.New()
        table_name = "att"
    End Sub

    Public Function uploadOne(id As Integer, file_index As Integer, Optional is_new As Boolean = False) As Hashtable
        Dim result As Hashtable = Nothing
        Dim filepath As String = Nothing
        If uploadFile(id, filepath, file_index, True) Then
            logger("uploaded to [" & filepath & "]")
            Dim ext As String = UploadUtils.getUploadFileExt(filepath)

            'TODO refactor in better way
            Dim file As HttpPostedFile = fw.req.Files(file_index)

            'update db with file information
            Dim fields As New Hashtable
            If is_new Then fields("iname") = file.FileName
            fields("iname") = file.FileName
            fields("fname") = file.FileName
            fields("fsize") = Utils.fileSize(filepath)
            fields("ext") = ext
            fields("status") = STATUS_ACTIVE 'finished upload - change status to active
            'turn on image flag if it's an image
            If UploadUtils.isUploadImgExtAllowed(ext) Then
                'if it's an image - turn on flag and resize for thumbs
                fields("is_image") = 1

                Utils.resizeImage(filepath, getUploadImgPath(id, "s", ext), MAX_THUMB_W_S, MAX_THUMB_H_S)
                Utils.resizeImage(filepath, getUploadImgPath(id, "m", ext), MAX_THUMB_W_M, MAX_THUMB_H_M)
                Utils.resizeImage(filepath, getUploadImgPath(id, "l", ext), MAX_THUMB_W_L, MAX_THUMB_H_L)
            End If

            Me.update(id, fields)
            fields("filepath") = filepath
            result = fields

#If is_S3 Then
            'if S3 configured - move file to S3 immediately
            moveToS3(id)
#End If
        End If
        Return result
    End Function

    'return id of the first successful upload
    ''' <summary>
    ''' mulitple files upload from Request.Files
    ''' </summary>
    ''' <param name="item">files to add to att table, can contain: table_name, item_id, att_categories_id</param>
    ''' <returns>db array list of added files information id, fname, fsize, ext, filepath</returns>
    Public Function uploadMulti(item As Hashtable) As ArrayList
        Dim result As New ArrayList

        For i = 0 To fw.req.Files.Count - 1
            Dim file = fw.req.Files(i)
            If file.ContentLength > 0 Then
                'add att db record
                Dim itemdb = item.Clone()
                itemdb("status") = 1 'under upload
                Dim id = Me.add(itemdb)

                Dim resone = Me.uploadOne(id, i, True)
                If resone IsNot Nothing Then
                    resone("id") = id
                    result.Add(resone)
                End If

            End If
        Next

        Return result
    End Function

    Public Function updateTmpUploads(files_code As String, att_table_name As String, item_id As Integer) As Boolean
        Dim where As New Hashtable
        where("table_name") = "tmp_" & att_table_name & "_" & files_code
        where("item_id") = 0
        db.update(table_name, New Hashtable From {{"table_name", att_table_name}, {"item_id", item_id}}, where)
        Return True
    End Function

    ''' <summary>
    ''' permanently removes any temporary uploads older than 48h
    ''' </summary>
    ''' <returns>number of uploads deleted</returns>
    Public Function cleanupTmpUploads() As Integer
        Dim rows = db.array("select * from " & db.q_ident(table_name) & " where add_time<DATEADD(hour, -48, getdate()) and (status=1 or table_name like 'tmp[_]%')")
        For Each row As Hashtable In rows
            Me.delete(row("id"), True)
        Next
        Return rows.Count
    End Function

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
        'also delete from related tables:
        'users.att_id -> null?
        'spages.head_att_id -> null?
        If is_perm Then
            'delete from att_table_link only if perm
            db.del(att_table_link, DB.h("att_id", id))
        End If

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
            deleteLocalFiles(id)
        End If

        MyBase.delete(id, is_perm)
    End Sub

    Public Sub deleteLocalFiles(id As Integer)
        Dim item As Hashtable = one(id)

        Dim filepath As String = getUploadImgPath(id, "", item("ext"))
        If filepath > "" Then File.Delete(filepath)
        'for images - also delete s/m thumbnails
        If item("is_image") = 1 Then
            For Each size As String In Utils.qw("s m l")
                filepath = getUploadImgPath(id, size, item("ext"))
                If filepath > "" Then File.Delete(filepath)
            Next
        End If
        'also remove folder - can't as there could be files with other ids
        'TODO -check if folder empty And remove
        'Dim folder = getUploadDir(id)
        'Directory.Delete(folder)
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
        Dim where As New Hashtable
        where("status") = STATUS_ACTIVE
        where("table_name") = table_name
        where("item_id") = item_id
        If is_image > -1 Then
            where("is_image") = is_image
        End If
        Return db.array(table_name, where, "id")
    End Function

    'like getAllByTableName, but also fills att_categories hash
    Public Function getAllByTableNameWithCategories(table_name As String, item_id As Integer, Optional is_image As Integer = -1) As ArrayList
        Dim rows = getAllByTableName(table_name, item_id, is_image)
        For Each row As Hashtable In rows
            Dim att_categories_id = Utils.f2int(row("att_categories_id"))
            If att_categories_id > 0 Then row("att_categories") = fw.model(Of AttCategories).one(att_categories_id)
        Next
        Return rows
    End Function

    'return one att record with additional check by table_name
    Public Function oneWithTableName(id As Integer, item_table_name As String) As Hashtable
        Dim row = one(id)
        If row("table_name") <> item_table_name Then row.Clear()
        Return row
    End Function

    'return one att record by table_name and item_id
    Public Function oneByTableName(item_table_name As String, item_id As Integer) As Hashtable
        Return db.row(table_name, New Hashtable From {
                      {"table_name", item_table_name},
                      {"item_id", item_id}
                      })
    End Function

    Function getS3KeyByID(id As String, Optional size As String = "") As String
        Dim sizestr = ""
        If size > "" Then sizestr = "_" & size

        Return Me.table_name & "/" & id & "/" & id & sizestr
    End Function

    'generate signed url and redirect to it, so user download directly from S3
    Public Sub redirectS3(item As Hashtable, Optional size As String = "")
#If is_S3 Then
        If fw.model(Of Users).meId() = 0 Then Throw New ApplicationException("Access Denied") 'denied for non-logged

        Dim url = fw.model(Of S3).getSignedUrl(getS3KeyByID(item("id"), size))

        fw.redirect(url)
#Else
        logger(LogLevel.WARN, "redirectS3 - S3 not enabled")
#End If
    End Sub

#If is_S3 Then

    Public Function moveToS3(id As Integer) As Boolean
        Dim result = True
        Dim item = one(id)
        If item("is_s3") = 1 Then Return True 'already in S3

        Dim model_s3 = fw.model(Of S3)
        'model_s3.createFolder(Me.table_name)
        'upload all sizes if exists
        'id=47 -> /47/47 /47/47_s /47/47_m /47/47_l
        For Each size As String In Utils.qw("&nbsp; s m l")
            size = Trim(size)
            Dim filepath As String = getUploadImgPath(id, size, item("ext"))
            If Not System.IO.File.Exists(filepath) Then Continue For

            Dim res = model_s3.uploadFilepath(getS3KeyByID(id, size), filepath, "inline")
            If res.HttpStatusCode <> Net.HttpStatusCode.OK Then
                result = False
                Exit For
            End If
        Next

        If result Then
            'mark as uploaded
            Me.update(id, New Hashtable From {{"is_s3", 1}})
            'remove local files
            deleteLocalFiles(id)
        End If

        Return True
    End Function

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

        Dim honlynames = Utils.qh(fieldnames)

        'create list of eligible file uploads, check for the ContentLength as any 'input type="file"' creates a System.Web.HttpPostedFile object even if the file was not attached to the input
        Dim afiles As New ArrayList
        If honlynames.Count > 0 Then
            'if we only need some fields - skip if not requested field
            For i = 0 To fw.req.Files.Count - 1
                If Not honlynames.ContainsKey(fw.req.Files.GetKey(i)) Then Continue For
                If fw.req.Files(i).ContentLength > 0 Then afiles.Add(fw.req.Files(i))
            Next
        Else
            'just add all files
            For i = 0 To fw.req.Files.Count - 1
                If fw.req.Files(i).ContentLength > 0 Then afiles.Add(fw.req.Files(i))
            Next
        End If

        'do nothing if empty file list
        If afiles.Count = 0 Then Return 0

        'upload files to the S3
        Dim model_s3 = fw.model(Of S3)

        'create /att folder
        model_s3.createFolder(Me.table_name)

        'upload files to S3
        For Each file In afiles
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
                Dim response = model_s3.uploadPostedFile(getS3KeyByID(att_id), file, "inline")

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
