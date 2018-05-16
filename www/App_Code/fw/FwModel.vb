' Fw Model base class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO

Public MustInherit Class FwModel
    Protected fw As FW
    Protected db As DB
    Public table_name As String = "" 'must be assigned in child class
    Public csv_export_fields As String = "*"
    Public csv_export_headers As String = ""

    Public Sub New(Optional fw As FW = Nothing)
        If fw IsNot Nothing Then
            Me.fw = fw
            Me.db = fw.db
        End If
    End Sub

    Public Overridable Sub init(fw As FW)
        Me.fw = fw
        Me.db = fw.db
    End Sub

    Public Overridable Function one(id As Integer) As Hashtable
        Dim item As Hashtable = fw.cache.getRequestValue("fwmodel_one_" & table_name & "#" & id)
        If IsNothing(item) Then
            Dim where As Hashtable = New Hashtable
            where("id") = id
            item = db.row(table_name, where)
            fw.cache.setRequestValue("fwmodel_one_" & table_name & "#" & id, item)
        End If
        Return item
    End Function

    Public Overridable Function iname(id As Integer) As String
        Dim row As Hashtable = one(id)
        Return row("iname")
    End Function

    'return standard list of id,iname where status=0 order by iname
    Public Overridable Function list() As ArrayList
        Dim sql As String = "select * from " & table_name & " where status=0 order by iname"
        Return db.array(sql)
    End Function

    'override if id/iname differs in table 
    Public Overridable Function listSelectOptions() As ArrayList
        Dim sql As String = "select id, iname from " & table_name & " where status=0 order by iname"
        Return db.array(sql)
    End Function

    'just return first row by iname field (you may want to make it unique)
    Public Overridable Function oneByIname(iname As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where("iname") = iname
        Return db.row(table_name, where)
    End Function

    'check if item exists for a given field
    Public Overridable Function isExistsByField(uniq_key As Object, not_id As Integer, field As String) As Boolean
        Dim val As String = db.value("select 1 from " & table_name & " where " & field & " = " & db.q(uniq_key) & " and id <>" & db.qi(not_id))
        If val = "1" Then
            Return True
        Else
            Return False
        End If
    End Function

    'check if item exists for a given iname
    Public Overridable Function isExists(uniq_key As Object, not_id As Integer) As Boolean
        Return isExistsByField(uniq_key, not_id, "iname")
    End Function

    'add new record and return new record id
    Public Overridable Function add(item As Hashtable) As Integer
        'item("add_time") = Now() 'not necessary because add_time field in db should have default value now() or getdate()
        If Not item.ContainsKey("add_users_id") AndAlso fw.SESSION("is_logged") Then item("add_users_id") = fw.SESSION("user_id")
        Dim id As Integer = db.insert(table_name, item)
        fw.logEvent(table_name & "_add", id)
        Return id
    End Function

    'update exising record
    Public Overridable Function update(id As Integer, item As Hashtable) As Boolean
        item("upd_time") = Now()
        If Not item.ContainsKey("upd_users_id") AndAlso fw.SESSION("is_logged") Then item("upd_users_id") = fw.SESSION("user_id")

        Dim where As New Hashtable
        where("id") = id
        db.update(table_name, item, where)

        fw.logEvent(table_name & "_upd", id)

        fw.cache.requestRemove("fwmodel_one_" & table_name & "#" & id) 'cleanup cache, so next one read will read new value
        Return True
    End Function

    'mark record as deleted (status=127) OR actually delete from db (if is_perm)
    Public Overridable Sub delete(id As Integer, Optional is_perm As Boolean = False)
        Dim where As New Hashtable
        where("id") = id

        If is_perm Then
            'place here code that remove related data
            db.del(table_name, where)
            fw.cache.requestRemove("fwmodel_one_" & table_name & "#" & id) 'cleanup cache, so next one read will read new value
        Else
            Dim vars As New Hashtable
            vars("status") = 127
            vars("upd_time") = Now()
            If fw.SESSION("is_logged") Then vars("add_users_id") = fw.SESSION("user_id")

            db.update(table_name, vars, where)
        End If
        fw.logEvent(table_name & "_del", id)
    End Sub

    'upload utils
    Public Overridable Function uploadFile(id As Integer, ByRef filepath As String, Optional input_name As String = "file1", Optional is_skip_check As Boolean = False) As Boolean
        Return UploadUtils.uploadFile(fw, table_name, id, filepath, input_name, is_skip_check)
    End Function

    'return upload dir for the module name and id related to FW.config("site_root")/upload
    ' id splitted to 1000
    Public Overridable Function getUploadDir(ByVal id As Long) As String
        Return UploadUtils.getUploadDir(fw, table_name, id)
    End Function

    Public Overridable Function getUploadUrl(ByVal id As Long, ByVal ext As String, Optional size As String = "") As String
        Return UploadUtils.getUploadUrl(fw, table_name, id, ext, size)
    End Function

    'removes all type of image files uploaded with thumbnails
    Public Overridable Function removeUpload(ByVal id As Long, ByVal ext As String) As Boolean
        Dim dir As String = getUploadDir(id)

        If UploadUtils.isUploadImgExtAllowed(ext) Then
            'if this is image - remove possibly created thumbs
            File.Delete(dir & "/" & id & "_l" & ext)
            File.Delete(dir & "/" & id & "_m" & ext)
            File.Delete(dir & "/" & id & "_s" & ext)
        End If

        'delete main file
        File.Delete(dir & "/" & id & ext)
        Return True
    End Function

    Public Overridable Function getUploadImgPath(ByVal id As Long, ByVal size As String, Optional ext As String = "") As String
        Return UploadUtils.getUploadImgPath(fw, table_name, id, size, ext)
    End Function

    'methods from fw - just for a covenience, so no need to use "fw.", as they are used quite frequently
    Public Overloads Sub logger(ByVal ParamArray args() As Object)
        If args.Length = 0 Then Return
        fw._logger(LogLevel.DEBUG, args)
    End Sub
    Public Overloads Sub logger(level As LogLevel, ByVal ParamArray args() As Object)
        If args.Length = 0 Then Return
        fw._logger(level, args)
    End Sub


    Public Overridable Function getSelectOptions(sel_id As String) As String
        Return FormUtils.selectOptions(Me.listSelectOptions(), sel_id)
    End Function

    Public Overridable Function getAutocompleteList(q As String) As ArrayList
        Dim sql As String = "select iname from " & table_name & " where status=0 and iname like " & db.q("%" & q & "%")
        Return db.col(sql)
    End Function

    'sel_ids - comma-separated ids
    Public Overridable Function getMultiList(sel_ids As String) As ArrayList
        Dim ids As New ArrayList(Split(sel_ids, ","))
        Dim rows As ArrayList = Me.list()
        For Each row As Hashtable In rows
            row("is_checked") = ids.Contains(row("id"))
        Next
        Return rows
    End Function

    ''' <summary>
    '''     return comma-separated ids of linked elements - TODO refactor to use arrays, not comma-separated string
    ''' </summary>
    ''' <param name="table_name">link table name that contains id_name and link_id_name fields</param>
    ''' <param name="id">main id</param>
    ''' <param name="id_name">field name for main id</param>
    ''' <param name="link_id_name">field name for linked id</param>
    ''' <returns></returns>
    Public Overridable Function getLinkIds(table_name As String, id As Integer, id_name As String, link_id_name As String) As String
        Dim where As New Hashtable
        where(id_name) = id
        Dim rows As ArrayList = db.array(table_name, where)
        Dim res As New ArrayList
        For Each row As Hashtable In rows
            res.Add(row(link_id_name))
        Next

        Return FormUtils.col2comma_str(res)
    End Function

    ''' <summary>
    '''  update (and add/del) linked table
    ''' </summary>
    ''' <param name="table_name">link table name that contains id_name and link_id_name fields</param>
    ''' <param name="id">main id</param>
    ''' <param name="id_name">field name for main id</param>
    ''' <param name="link_id_name">field name for linked id</param>
    ''' <param name="linked_keys">hashtable with keys as link id (as passed from web)</param>
    Public Overridable Sub updateLinked(table_name As String, id As Integer, id_name As String, link_id_name As String, linked_keys As Hashtable)
        Dim fields As New Hashtable
        Dim where As New Hashtable

        'set all fields as under update
        fields("status") = 1
        where(id_name) = id
        db.update(table_name, fields, where)

        If linked_keys IsNot Nothing Then
            For Each link_id As String In linked_keys.Keys
                fields = New Hashtable
                fields(id_name) = id
                fields(link_id_name) = link_id
                fields("status") = 0

                where = New Hashtable
                where(id_name) = id
                where(link_id_name) = link_id
                db.update_or_insert(table_name, fields, where)
            Next
        End If

        'remove those who still not updated (so removed)
        where = New Hashtable
        where(id_name) = id
        where("status") = 1
        db.del(table_name, where)
    End Sub


    Public Overridable Function addOrUpdateQuick(iname As String) As Integer
        Dim result As Integer
        Dim item As Hashtable = Me.oneByIname(iname)
        If item.ContainsKey("id") Then
            'exists
            result = item("id")
        Else
            'not exists - add new
            item = New Hashtable
            item("iname") = iname
            result = Me.add(item)
        End If
        Return result
    End Function

    Public Overridable Function getCSVExport() As StringBuilder
        Dim rows As ArrayList = db.array("select " & csv_export_fields & " from " & table_name & " where status=0")
        Return Utils.getCSVExport(csv_export_headers, csv_export_fields, rows)
    End Function
End Class

