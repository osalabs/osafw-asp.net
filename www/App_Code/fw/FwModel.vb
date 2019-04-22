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

    Public field_id As String = "id" 'default primary key name
    Public field_iname As String = "iname"

    'default field names. If you override it and make empty - automatic processing disabled
    Public field_status As String = "status"
    Public field_add_users_id As String = "add_users_id"
    Public field_upd_users_id As String = "upd_users_id"
    Public field_upd_time As String = "upd_time"

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
            where(Me.field_id) = id
            item = db.row(table_name, where)
            fw.cache.setRequestValue("fwmodel_one_" & table_name & "#" & id, item)
        End If
        Return item
    End Function

    Public Overridable Function iname(id As Integer) As String
        Dim row As Hashtable = one(id)
        Return row(field_iname)
    End Function

    'return standard list of id,iname where status=0 order by iname
    Public Overridable Function list() As ArrayList
        Dim where As New Hashtable
        If field_status > "" Then where("status") = 0
        Return db.array(table_name, where, field_iname)
    End Function

    'override if id/iname differs in table
    'params - to use - override in your model
    Public Overridable Function listSelectOptions(Optional params As Object = Nothing) As ArrayList
        Dim where = ""
        If field_status > "" Then where = " where status=0 "
        Dim sql As String = "select " & field_id & " as id, " & field_iname & " as iname from " & table_name & where & " order by " & field_iname
        Return db.array(sql)
    End Function

    'return count of all non-deleted
    Public Function getCount() As Integer
        Dim where = ""
        If field_status > "" Then where = " where status<>127 "
        Return db.value("select count(*) from " & table_name & where)
    End Function

    'just return first row by iname field (you may want to make it unique)
    Public Overridable Function oneByIname(iname As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where(field_iname) = iname
        Return db.row(table_name, where)
    End Function

    'check if item exists for a given field
    Public Overridable Function isExistsByField(uniq_key As Object, not_id As Integer, field As String) As Boolean
        Dim val As String = db.value("select 1 from " & table_name & " where " & field & " = " & db.q(uniq_key) & " and " & db.q_ident(field_id) & " <>" & db.qi(not_id))
        If val = "1" Then
            Return True
        Else
            Return False
        End If
    End Function

    'check if item exists for a given iname
    Public Overridable Function isExists(uniq_key As Object, not_id As Integer) As Boolean
        Return isExistsByField(uniq_key, not_id, field_iname)
    End Function

    'add new record and return new record id
    Public Overridable Function add(item As Hashtable) As Integer
        'item("add_time") = Now() 'not necessary because add_time field in db should have default value now() or getdate()
        If field_add_users_id > "" AndAlso Not item.ContainsKey(field_add_users_id) AndAlso fw.SESSION("is_logged") Then item(field_add_users_id) = fw.SESSION("user_id")
        Dim id As Integer = db.insert(table_name, item)
        fw.logEvent(table_name & "_add", id)
        Return id
    End Function

    'update exising record
    Public Overridable Function update(id As Integer, item As Hashtable) As Boolean
        If field_upd_time > "" Then item("upd_time") = Now()
        If field_upd_users_id > "" AndAlso Not item.ContainsKey(field_upd_users_id) AndAlso fw.SESSION("is_logged") Then item(field_upd_users_id) = fw.SESSION("user_id")

        Dim where As New Hashtable
        where(Me.field_id) = id
        db.update(table_name, item, where)

        fw.logEvent(table_name & "_upd", id)

        fw.cache.requestRemove("fwmodel_one_" & table_name & "#" & id) 'cleanup cache, so next one read will read new value
        Return True
    End Function

    'mark record as deleted (status=127) OR actually delete from db (if is_perm or status field not defined for this model table)
    Public Overridable Sub delete(id As Integer, Optional is_perm As Boolean = False)
        Dim where As New Hashtable
        where(Me.field_id) = id

        If is_perm OrElse field_status = "" Then
            'place here code that remove related data
            db.del(table_name, where)
            fw.cache.requestRemove("fwmodel_one_" & table_name & "#" & id) 'cleanup cache, so next one read will read new value
        Else
            Dim vars As New Hashtable
            vars(field_status) = 127
            If field_upd_time > "" Then vars(field_upd_time) = Now()
            If field_add_users_id > "" AndAlso fw.SESSION("is_logged") Then vars(field_add_users_id) = fw.SESSION("user_id")

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
        Dim where = field_iname & " like " & db.q("%" & q & "%")
        If field_status > "" Then where &= " and status<>127 "

        Dim sql As String = "select " & field_iname & " as iname from " & table_name & " where " & where
        Return db.col(sql)
    End Function

    'sel_ids - comma-separated ids
    'params - to use - override in your model
    Public Overridable Function getMultiList(sel_ids As String, Optional params As Object = Nothing) As ArrayList
        Dim ids As New ArrayList(Split(sel_ids, ","))
        Dim rows As ArrayList = Me.list()
        For Each row As Hashtable In rows
            row("is_checked") = ids.Contains(row(Me.field_id))
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


    Public Overridable Function findOrAddByIname(iname As String, ByRef Optional is_added As Boolean = False) As Integer
        Dim result As Integer
        Dim item As Hashtable = Me.oneByIname(iname)
        If item.ContainsKey(Me.field_id) Then
            'exists
            result = item(Me.field_id)
        Else
            'not exists - add new
            item = New Hashtable
            item(field_iname) = iname
            result = Me.add(item)
            is_added = True
        End If
        Return result
    End Function

    Public Overridable Function getCSVExport() As StringBuilder
        Dim where = ""
        If field_status > "" Then where = " where status=0 "

        Dim rows As ArrayList = db.array("select " & csv_export_fields & " from " & table_name & where)
        Return Utils.getCSVExport(csv_export_headers, csv_export_fields, rows)
    End Function
End Class

