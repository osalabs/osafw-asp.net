' LookupManager Tables model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2016 Oleg Savchuk www.osalabs.com

Public Class LookupManagerTables
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "lookup_manager_tables"
    End Sub

    'just return first row by tname field (you may want to make it unique)
    'CACHED
    Public Overridable Function oneByTname(tname As String) As Hashtable
        Dim item As Hashtable = fw.cache.getRequestValue("LookupManagerTables_one_by_tname_" & table_name & "#" & tname)
        If IsNothing(item) Then
            Dim where As Hashtable = New Hashtable
            where("tname") = tname
            item = db.row(table_name, where)
            fw.cache.setRequestValue("LookupManagerTables_one_by_tname_" & table_name & "#" & tname, item)
        End If
        Return item
    End Function

    'return table columns from database
    'no identity column returned
    'no timestamp, image, varbinary returned (as not supported by UI)
    'filtered by defs(columns)
    'added custom names, types and grouping info - if defined
    Public Function getColumns(defs As Hashtable) As ArrayList
        Dim result As New ArrayList

        Dim custom_columns As New Hashtable
        Dim ix_custom_columns As New Hashtable
        If defs("columns") > "" Then 'custom columns defined, prepare custom info
            Dim custom_cols = Utils.commastr2hash(defs("columns"), "123...")
            For Each key As String In custom_cols.Keys
                Dim h As New Hashtable
                h("index") = custom_cols(key)
                h("iname") = key 'default display name is column name
                h("itype") = "" 'no default input type
                h("igroup") = "" 'no default group
                custom_columns(key) = h

                ix_custom_columns(h("index")) = h 'build inverted index
            Next
            'custom names
            If defs("column_names") > "" Then
                Dim custom_names As New ArrayList(Split(defs("column_names"), ","))
                For i = 0 To custom_names.Count - 1
                    ix_custom_columns(i)("iname") = custom_names(i)
                Next
            End If
            'custom types
            If defs("column_types") > "" Then
                Dim custom_types As New ArrayList(Split(defs("column_types"), ","))
                For i = 0 To custom_types.Count - 1
                    ix_custom_columns(i)("itype") = Trim(custom_types(i))
                Next
            End If

            'groups
            If defs("column_groups") > "" Then
                'Dim groups As Hashtable = Utils.commastr2hash(defs("groups"), "123...")
                Dim custom_groups As New ArrayList(Split(defs("column_groups"), ","))
                For i = 0 To custom_groups.Count - 1
                    ix_custom_columns(i)("igroup") = custom_groups(i)
                Next
            End If
        End If

        Dim cols As ArrayList = db.load_table_schema_full(defs("tname"))
        For Each col As Hashtable In cols
            Dim coltype As String = col("type")
            'skip unsupported (binary) fields
            'identity field also skipped as not updateable
            If col("is_identity") = "1" OrElse coltype = "timestamp" OrElse coltype = "image" OrElse coltype = "varbinary" Then
                Continue For
            End If

            'add/override custom info
            If custom_columns.Count > 0 Then
                Dim cc As Hashtable = custom_columns(col("name"))
                If cc Is Nothing Then
                    'skip this field as not custom defined
                    Continue For
                Else
                    Utils.mergeHash(col, cc)
                End If
            Else
                'defaults
                'col("index") = 0
                col("iname") = Utils.name2human(col("name")) 'default display name is column name
                col("itype") = "" 'no default input type
                col("igroup") = "" 'no default group
            End If
            result.Add(col)
        Next

        If custom_columns.Count > 0 Then
            'if custom columns - return columns sorted according to custom list
            'sort with LINQ
            Dim query = From col As Hashtable In result
                        Order By col("index")
                        Select col

            Dim sorted_result As New ArrayList
            For Each h As Hashtable In query
                sorted_result.Add(h)
            Next
            Return sorted_result
        Else
            Return result
        End If
    End Function

    'return "id" or customer column_id defined in defs
    Public Function getColumnId(defs As Hashtable) As String
        If defs("column_id") > "" Then
            Return defs("column_id")
        Else
            Return "id"
        End If
    End Function

    Public Function getLookupSelectOptions(itype_lookup As String, sel_id As Object) As String
        Dim lutable As String = "", lufields As String = ""
        Utils.split2("\.", itype_lookup, lutable, lufields)

        Dim idfield = "", inamefield = ""
        Utils.split2(":", lufields, idfield, inamefield)

        Dim sql As String = "select " & db.q_ident(idfield) & " as id, " & db.q_ident(inamefield) & " as iname from " & db.q_ident(lutable) & " order by 1"
        Return FormUtils.selectOptions(db.array(sql), sel_id)
    End Function

    Public Function getLookupValue(itype_lookup As String, sel_id As Object) As String
        Dim lutable As String = "", lufields As String = ""
        Utils.split2("\.", itype_lookup, lutable, lufields)

        Dim idfield = "", inamefield = ""
        Utils.split2(":", lufields, idfield, inamefield)

        Dim where As New Hashtable From {
                {idfield, sel_id}
            }
        Return db.value(lutable, where, inamefield)

    End Function


End Class
