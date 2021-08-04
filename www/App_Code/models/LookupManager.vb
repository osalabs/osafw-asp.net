' LookupManager model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2016 Oleg Savchuk www.osalabs.com

Public Class LookupManager
    Inherits FwModel

    'system columns that not need to be shown to user as is
    Public SYS_COLS As New Hashtable From {
            {"add_time", True},
            {"add_users_id", True},
            {"upd_time", True},
            {"upd_users_id", True}
        }

    Public Sub New()
        MyBase.New()
        table_name = "xxx"
    End Sub

    'return top X rows (default 1) from table tname
    Public Overridable Function topByTname(tname As String, Optional top_number As Integer = 1) As Hashtable
        If tname = "" Then Throw New ApplicationException("Wrong topByTname params")

        Return db.row("select TOP " & top_number & " * from " & tname)
    End Function

    Public Overridable Function maxIdByTname(tname As String) As Integer
        If tname = "" Then Throw New ApplicationException("Wrong maxIdByTname params")

        Dim defs As Hashtable = fw.model(Of LookupManagerTables).oneByTname(tname)
        If defs.Count = 0 Then Throw New ApplicationException("Wrong lookup table name")

        Dim id_field = fw.model(Of LookupManagerTables).getColumnId(defs)
        Return db.value("SELECT MAX(" & db.q_ident(id_field) & ") from " & db.q_ident(tname))
    End Function

    Public Overridable Function oneByTname(tname As String, id As Integer) As Hashtable
        If tname = "" OrElse id = 0 Then Throw New ApplicationException("Wrong oneByTname params")

        Dim defs As Hashtable = fw.model(Of LookupManagerTables).oneByTname(tname)
        If defs.Count = 0 Then Throw New ApplicationException("Wrong lookup table name")

        Dim where As New Hashtable
        where(fw.model(Of LookupManagerTables).getColumnId(defs)) = id
        Return db.row(tname, where)
    End Function


    'add new record and return new record id
    Public Overridable Function addByTname(tname As String, item As Hashtable) As Integer
        If tname = "" Then Throw New ApplicationException("Wrong update_by_tname params")
        Dim defs As Hashtable = fw.model(Of LookupManagerTables).oneByTname(tname)
        If defs.Count = 0 Then Throw New ApplicationException("Wrong lookup table name")

        If Not defs("list_columns") > "" Then
            'if no list cols - it's std table - add std fields
            If Not item.ContainsKey("add_users_id") AndAlso fw.SESSION("is_logged") Then item("add_users_id") = fw.model(Of Users).meId()
        End If

        Dim id As Integer = db.insert(tname, item)
        fw.logEvent(tname & "_add", id)
        Return id
    End Function

    'update exising record
    Public Overridable Function updateByTname(tname As String, id As Integer, item As Hashtable, Optional md5 As String = "") As Boolean
        If tname = "" OrElse id = 0 Then Throw New ApplicationException("Wrong update_by_tname params")
        Dim defs As Hashtable = fw.model(Of LookupManagerTables).oneByTname(tname)
        If defs.Count = 0 Then Throw New ApplicationException("Wrong lookup table name")
        Dim id_fname As String = fw.model(Of LookupManagerTables).getColumnId(defs)

        'also we need include old fields into where just because id by sort is not robust enough
        Dim itemold = oneByTname(tname, id)
        'If Not defs("list_columns") > "" Then
        '    'remove syscols
        '    Dim itemold2 As New Hashtable
        '    For Each key In itemold.Keys
        '        If SYS_COLS.ContainsKey(key) Then Continue For
        '        itemold2(key) = itemold(key)
        '    Next
        '    itemold = itemold2
        'End If
        If md5 > "" Then
            'additionally check we got right record by comparing md5
            If md5 <> getRowMD5(itemold) Then Throw New ApplicationException("Cannot update database. Wrong checksum. Probably someone else already updated data you are trying to edit.")
        End If

        itemold.Remove(id_fname)
        itemold.Remove("SSMA_TimeStamp") 'remove timestamp fields, it was created during migration from Access

        'now compare new values with old values and save only values that are different
        'so if nothing changed - no db update performed
        'logger("OLD")
        'logger(itemold)
        'logger("NEW")
        'logger(item)
        Dim item_save As New Hashtable
        For Each key As String In item.Keys
            If itemold(key).ToString() <> item(key).ToString() Then
                item_save(key) = item(key)
            End If
        Next
        'logger("NEW SAVE")
        'logger(item_save)

        If item_save.Count > 0 Then
            Dim where As New Hashtable
            where(id_fname) = id

            If Not defs("list_columns") > "" Then
                'if no list cols - it's std table - add std fields
                If Not item_save.ContainsKey("upd_time") Then item_save("upd_time") = Now()
                If Not item_save.ContainsKey("upd_users_id") AndAlso fw.SESSION("is_logged") Then item_save("upd_users_id") = fw.model(Of Users).meId()
            End If

            db.update(tname, item_save, where)

            fw.logEvent(tname & "_upd", id)
            Return True
        Else
            Return False
        End If
    End Function

    ' delete from db
    Public Overridable Sub deleteByTname(tname As String, id As Integer, Optional md5 As String = "")
        If tname = "" OrElse id = 0 Then Throw New ApplicationException("Wrong update_by_tname params")
        Dim defs As Hashtable = fw.model(Of LookupManagerTables).oneByTname(tname)
        If defs.Count = 0 Then Throw New ApplicationException("Wrong lookup table name")
        Dim id_fname As String = fw.model(Of LookupManagerTables).getColumnId(defs)

        'also we need include old fields into where just because id by sort is not robust enough
        Dim itemold = oneByTname(tname, id)
        If md5 > "" Then
            'additionally check we got right record by comparing md5
            If md5 <> getRowMD5(itemold) Then Throw New ApplicationException("Cannot delete from database. Wrong checksum. Probably someone else already updated data you are trying to edit.")
        End If

        Dim where As New Hashtable
        where(id_fname) = id
        db.del(tname, where)

        fw.logEvent(tname & "_del", id)
    End Sub

    'calculate md5 for all values from hashtable
    'values sorted by keyname before calculating
    Friend Function getRowMD5(row As Hashtable) As String
        'sort with LINQ
        Dim sorted_keys = From k In row.Keys
                          Order By k
                          Select k

        Dim str As New StringBuilder
        'logger("calc id for: " & row("_RowNumber"))
        For Each fieldname As String In sorted_keys
            'logger(fieldname)
            If fieldname = "_RowNumber" Then Continue For
            str.AppendLine(row(fieldname))
        Next
        'logger(row("id"))
        'logger(str.ToString())
        'logger(Utils.md5(str.ToString()))
        Return Utils.md5(str.ToString())
    End Function

    Function filterOutSysCols(cols As ArrayList) As ArrayList
        Dim result As New ArrayList

        For Each col As Hashtable In cols
            If SYS_COLS.ContainsKey(col("name")) Then Continue For
            result.Add(col)
        Next

        Return result
    End Function

End Class
