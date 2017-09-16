' Att model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO

Public Class Att
    Inherits FwModel
    Public MIME_MAP As String = "doc|application/msword docx|application/msword xls|application/vnd.ms-excel xlsx|application/vnd.ms-excel ppt|application/vnd.ms-powerpoint pptx|application/vnd.ms-powerpoint pdf|application/pdf html|text/html zip|application/x-zip-compressed jpg|image/jpeg jpeg|image/jpeg gif|image/gif png|image/png wmv|video/x-ms-wmv avi|video/x-msvideo"
    Public att_table_link As String = "att_table_link"

    Public Sub New()
        MyBase.New()
        table_name = "att"
    End Sub

    'add/update att_table_links
    Public Sub update_att_links(table_name As String, id As Integer, form_att As Hashtable)
        If form_att Is Nothing Then Exit Sub

        Dim me_id As Integer = fw.model(Of Users).me_id()

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
                fields("add_user_id") = me_id
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
    Public Function get_url(id As Integer, Optional size As String = "") As String
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
    Public Overloads Function get_url_direct(id As Integer, Optional size As String = "") As String
        Dim item As Hashtable = one(id)
        If item.Count = 0 Then Return ""

        Return get_url_direct(item, size)
    End Function

    'if you already have item, must contain: item("id"), item("ext")
    Public Overloads Function get_url_direct(item As Hashtable, Optional size As String = "") As String
        Return get_upload_url(item("id"), item("ext"), size)
    End Function


    'IN: extension - doc, jpg, ... (dot is optional)
    'OUT: mime type or application/octetstream if not found
    Public Function get_mime4ext(ext As String) As String
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
        Dim filepath As String = get_upload_img_path(id, "", item("ext"))
        If filepath>"" then File.Delete(filepath)
        'for images - also delete s/m thumbnails
        If item("is_image") = 1 Then
            filepath=get_upload_img_path(id, "s", item("ext"))
            If filepath>"" then File.Delete(filepath)
            filepath=get_upload_img_path(id, "m", item("ext"))
            If filepath>"" then File.Delete(filepath)
        End If

        MyBase.delete(id, is_perm)
    End Sub

    'check access rights for current user for the file by id
    'generate exception
    Public Sub check_access_rights(id As Integer)
        Dim result As Boolean = True
        Dim item As Hashtable = one(id)

        Dim user_access_level As Integer = fw.SESSION("access_level")
        Dim user As Hashtable = fw.SESSION("user")

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
    Public Sub transmit_file(id As Integer, Optional size As String = "", Optional disposition As String = "attachment")
        Dim item As Hashtable = one(id)
        If size <> "s" AndAlso size <> "m" Then size = ""

        If item("id") > 0 Then
            check_access_rights(item("id"))
            fw.resp.Cache.SetCacheability(HttpCacheability.Private) 'use public only if all uploads are public
            fw.resp.Cache.SetExpires(DateTime.Now.AddDays(30)) 'cache for 30 days, this allows browser not to send any requests to server during this period (unless F5)
            fw.resp.Cache.SetMaxAge(New TimeSpan(30, 0, 0, 0))

            Dim filepath As String = get_upload_img_path(id, size, item("ext"))
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
                Dim ext As String = UploadUtils.get_upload_file_ext(filename)

                fw.resp.AppendHeader("Content-type", get_mime4ext(ext))
                fw.resp.AppendHeader("Content-Disposition", disposition & "; filename=""" & filename & """")

                fw.resp.TransmitFile(filepath)
            End If
        Else
            Throw New ApplicationException("No file specified")
        End If
    End Sub

    'return all att files linked via att_table_link
    ' is_image = -1 (all - files and images), 0 (files only), 1 (images only)
    Public Function get_all_linked(table_name As String, id As Integer, Optional is_image As Integer = -1) As ArrayList
        Dim where As String = ""
        If is_image > -1 Then
            where &= " and a.is_image=" & is_image
        End If
        Return db.array("select a.* " & _
                    " from " & att_table_link & " atl, att a " & _
                    " where atl.table_name=" & db.q(table_name) & _
                    " and atl.item_id=" & db.qi(id) & _
                    " and a.id=atl.att_id" & _
                    where & _
                    " order by a.id ")
    End Function


    'return first att image linked via att_table_link
    Public Function get_first_linked_image(table_name As String, id As Integer) As Hashtable
        Return db.row("select top 1 a.* " & _
                    " from " & att_table_link & " atl, att a " & _
                    " where atl.table_name=" & db.q(table_name) & _
                    " and atl.item_id=" & db.qi(id) & _
                    " and a.id=atl.att_id" & _
                    " and a.is_image=1 " & _
                    " order by a.id ")
    End Function

    'return all att images linked via att_table_link
    Public Function get_all_linked_images(table_name As String, id As Integer) As ArrayList
        Return get_all_linked(table_name, id, 1)
    End Function
End Class
