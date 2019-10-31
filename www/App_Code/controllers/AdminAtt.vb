' Admin Att controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminAttController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 80

    Protected model As New Att
    Private Shared MAX_THUMB_W_S As Integer = 180
    Private Shared MAX_THUMB_H_S As Integer = 180
    Private Shared MAX_THUMB_W_M As Integer = 315
    Private Shared MAX_THUMB_H_M As Integer = 315

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        required_fields = "iname" 'default required fields, space-separated
        base_url = "/Admin/Att" 'base url for the controller
    End Sub

    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable

        'get filters
        Dim f As Hashtable = initFilter()

        'sorting
        If f("sortby") = "" Then f("sortby") = "iname"
        If f("sortdir") <> "desc" Then f("sortdir") = "asc"
        Dim SORTSQL As Hashtable = Utils.qh("id|id iname|iname add_time|add_time fsize|fsize ext|ext category|att_categories_id status|status")

        Dim where As String = " status = 0 and table_name='' "
        If f("s") > "" Then
            where &= " and (iname like " & db.q("%" & f("s") & "%") & _
                    " or fname like " & db.q("%" & f("s") & "%") & ")"
        End If

        If f("att_categories_id") > "" Then
            where &= " and att_categories_id=" & Utils.f2int(f("att_categories_id"))
        End If

        hf("count") = db.value("select count(*) from " & model.table_name & " where " & where)
        If hf("count") > 0 Then
            Dim offset As Integer = f("pagenum") * f("pagesize")
            Dim limit As Integer = f("pagesize")
            Dim orderby As String = SORTSQL(f("sortby"))
            If Not orderby > "" Then Throw New Exception("No orderby defined for [" & f("sortby") & "]")
            If f("sortdir") = "desc" Then
                If InStr(orderby, ",") Then orderby = Replace(orderby, ",", " desc,")
                orderby = orderby & " desc"
            End If

            'offset+1 because _RowNumber starts from 1
            Dim sql As String = "SELECT TOP " & limit & " * " & _
                            " FROM (" & _
                            "   SELECT *, ROW_NUMBER() OVER (ORDER BY " & orderby & ") AS _RowNumber" & _
                            "   FROM " & model.table_name & _
                            "   WHERE " & where & _
                            ") tmp" & _
                        " WHERE _RowNumber >= " & (offset + 1) & _
                        " ORDER BY " & orderby

            hf("list_rows") = db.array(sql)
            hf("pager") = FormUtils.getPager(hf("count"), f("pagenum"), f("pagesize"))

            'add/modify rows from db
            For Each row As Hashtable In hf("list_rows")
                row("fsize_human") = Utils.bytes2str(row("fsize"))
                If row("is_image") = 1 Then row("url_s") = model.getUrl(row("id"), "s")
                row("url_direct") = model.getUrlDirect(row("id"))

                If row("att_categories_id") > "" Then row("cat") = fw.model(Of AttCategories).one(row("att_categories_id"))
            Next
        End If
        hf("f") = f

        hf("select_att_categories_ids") = fw.model(Of AttCategories).listSelectOptions()

        Return hf
    End Function

    Public Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        Dim hf As Hashtable = New Hashtable
        Dim item As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        If fw.cur_method = "GET" Then 'read from db
            If id > 0 Then
                item = model.one(id)
            Else
                'set defaults here
                item = New Hashtable
            End If
        Else
            'read from db
            item = model.one(id)

            'and merge new values from the form
            Utils.mergeHash(item, reqh("item"))
            'here make additional changes if necessary
        End If
        hf("fsize_human") = Utils.bytes2str(item("fsize"))
        hf("url") = model.getUrl(id)
        If item("is_image") = 1 Then hf("url_m") = model.getUrl(id, "m")

        hf("select_options_att_categories_id") = fw.model(Of AttCategories).listSelectOptions()

        hf("add_users_id_name") = fw.model(Of Users).iname(item("add_users_id"))
        hf("upd_users_id_name") = fw.model(Of Users).iname(item("upd_users_id"))

        hf("id") = id
        hf("i") = item
        If fw.FERR.Count > 0 Then logger(fw.FERR)

        Return hf
    End Function


    Public Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        Dim hf As New Hashtable
        Dim item As Hashtable = reqh("item")
        If item Is Nothing Then
            item = New Hashtable
        End If

        Dim id As Integer = Utils.f2int(form_id)
        Dim is_image As Integer = 0

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim itemold As Hashtable = model.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Utils.qw("att_categories_id iname status"))
            If Not itemdb("iname") > "" Then itemdb("iname") = "new file upload"

            Dim is_add As Boolean = False
            If id > 0 Then
                model.update(id, itemdb)
                fw.FLASH("updated", 1)
            Else
                is_add = True
                id = model.add(itemdb)
                fw.FLASH("added", 1)
            End If

            'Proceed upload
            Dim filepath As String = Nothing
            If model.uploadFile(id, filepath, "file1", True) Then
                logger("uploaded to [" & filepath & "]")
                Dim ext As String = UploadUtils.getUploadFileExt(filepath)

                'TODO refactor in better way
                Dim file As HttpPostedFile = fw.req.Files("file1")

                'update db with file information
                Dim vars As New Hashtable
                If is_add Then vars("iname") = file.FileName
                vars("fname") = file.FileName
                vars("fsize") = Utils.fileSize(filepath)
                vars("ext") = ext
                'turn on image flag if it's an image
                If UploadUtils.isUploadImgExtAllowed(ext) Then
                    'if it's an image - turn on flag and resize for thumbs
                    vars("is_image") = 1
                    is_image = 1

                    Utils.resizeImage(filepath, model.getUploadImgPath(id, "s", ext), MAX_THUMB_W_S, MAX_THUMB_H_S)
                    Utils.resizeImage(filepath, model.getUploadImgPath(id, "m", ext), MAX_THUMB_W_M, MAX_THUMB_H_M)
                End If

                Dim where As New Hashtable
                where("id") = id
                db.update("att", vars, where)
            End If

            'if select in popup - return json
            hf("success") = True
            hf("id") = id
            hf("iname") = itemdb("iname")
            hf("url") = model.getUrlDirect(id)
            hf("is_image") = is_image
            hf("_json") = True

            'otherwise just redirect
            hf("_redirect") = base_url & "/" & id & "/edit"

        Catch ex As ApplicationException
            hf("success") = False
            hf("err_msg") = ex.Message
            hf("_json") = True

            fw.G("err_msg") = ex.Message
            hf("_route_redirect") = New Hashtable From {
                    {"method", "ShowForm"},
                    {"args", New String() {id}}
                }
        End Try

        Return hf
    End Function

    Public Function Validate(id As Integer, item As Hashtable) As Boolean
        Dim result As Boolean = True
        'only require file during first upload
        'only require iname during update
        Dim itemdb As Hashtable
        If id > 0 Then
            itemdb = model.one(id)
            result = result And validateRequired(item, Utils.qw(required_fields))
        Else
            itemdb = New Hashtable
            itemdb("fsize") = 0
        End If

        If itemdb("fsize") = 0 Then
            Dim file As HttpPostedFile = fw.req.Files("file1")
            If IsNothing(file) OrElse file.ContentLength = 0 Then
                result = False
                fw.FERR("file1") = "NOFILE"
            End If
        End If

        If Not result Then fw.FERR("REQUIRED") = True

        If fw.FERR.Count > 0 AndAlso Not fw.FERR.ContainsKey("REQ") Then
            fw.FERR("INVALID") = 1
        End If

        If Not result Then Throw New ApplicationException("")
        Return True
    End Function

    Public Function ShowDeleteAction(ByVal form_id As String) As Hashtable
        Dim hf As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        hf("i") = model.one(id)
        Return hf
    End Function

    Public Sub DeleteAction(ByVal form_id As String)
        Dim id As Integer = Utils.f2int(form_id)

        model.delete(id)
        fw.FLASH("onedelete", 1)
        fw.redirect(base_url)
    End Sub

    Public Sub SaveMultiAction()
        Dim cbses As Hashtable = reqh("cb")
        If cbses Is Nothing Then cbses = New Hashtable
        Dim ctr As Integer = 0

        For Each id As String In cbses.Keys
            If fw.FORM.ContainsKey("delete") Then
                model.delete(id)
                ctr += 1
            End If
        Next

        fw.FLASH("multidelete", ctr)
        fw.redirect(base_url)
    End Sub

    Public Function SelectAction() As Hashtable
        Dim hf As New Hashtable
        Dim category_icode As String = reqs("category")
        Dim att_categories_id As String = reqi("att_categories_id")
        Dim att_cat As New Hashtable

        Dim where As New Hashtable
        where("status") = 0
        If category_icode > "" Then
            att_cat = fw.model(Of AttCategories).oneByIcode(category_icode)
            If att_cat.Count > 0 Then
                att_categories_id = att_cat("id")
                where("att_categories_id") = att_categories_id
            End If
        End If
        If att_categories_id > 0 Then
            where("att_categories_id") = att_categories_id
        End If

        Dim rows As ArrayList = db.array(model.table_name, where, "add_time desc")
        For Each row As Hashtable In rows
            row("direct_url") = model.getUrlDirect(row)
        Next
        hf("att_dr") = rows
        hf("select_att_categories_id") = fw.model(Of AttCategories).listSelectOptions()
        hf("att_categories_id") = att_categories_id

        Return hf
    End Function
End Class
