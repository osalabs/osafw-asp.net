' Framework DB class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2019 Oleg Savchuk www.osalabs.com

Imports System.Data.OleDb
Imports System.Data.SqlClient
Imports System.Data
Imports System.Data.Common
Imports System.IO
Imports System.Data.Odbc

Public Enum DBOps As Integer
    [EQ]            '=
    [NOT]           '<>
    [LE]            '<=
    [LT]            '<
    [GE]            '>=
    [GT]            '>
    [ISNULL]        'IS NULL
    [ISNOTNULL]     'IS NOT NULL
    [IN]            'IN
    [NOTIN]         'NOT IN
    [LIKE]          'LIKE
    [NOTLIKE]       'NOT LIKE
    [BETWEEN]       'BETWEEN
End Enum

'describes DB operation
Public Class DBOperation
    Public op As DBOps
    Public opstr As String 'string value for op
    Public is_value As Boolean = True 'if false - operation is unary (no value)
    Public value As Object 'can be array for IN, NOT IN, OR
    Public quoted_value As String
    Public Sub New(op As DBOps, Optional value As Object = Nothing)
        Me.op = op
        Me.setOpStr()
        Me.value = value
    End Sub
    Public Sub setOpStr()
        Select Case op
            Case DBOps.ISNULL
                opstr = "IS NULL"
                is_value = False
            Case DBOps.ISNOTNULL
                opstr = "IS NOT NULL"
                is_value = False
            Case DBOps.EQ
                opstr = "="
            Case DBOps.NOT
                opstr = "<>"
            Case DBOps.LE
                opstr = "<="
            Case DBOps.LT
                opstr = "<"
            Case DBOps.GE
                opstr = ">="
            Case DBOps.GT
                opstr = ">"
            Case DBOps.IN
                opstr = "IN"
            Case DBOps.NOTIN
                opstr = "NOT IN"
            Case DBOps.LIKE
                opstr = "LIKE"
            Case DBOps.NOTLIKE
                opstr = "NOT LIKE"
            Case DBOps.BETWEEN
                opstr = "BETWEEN"
            Case Else
                Throw New ApplicationException("Wrong DB OP")
        End Select
    End Sub
End Class

Public Class DB
    Implements IDisposable
    Private Shared schemafull_cache As Hashtable 'cache for the full schema, lifetime = app lifetime
    Private Shared schema_cache As Hashtable 'cache for the schema, lifetime = app lifetime

    Public Shared SQL_QUERY_CTR As Integer = 0 'counter for SQL queries during request

    Private ReadOnly fw As FW 'for now only used for: fw.logger and fw.cache (for request-level cacheing of multi-db connections)

    Public db_name As String = ""
    Public dbtype As String = "SQL"
    Private ReadOnly conf As New Hashtable  'config contains: connection_string, type
    Private ReadOnly connstr As String = ""

    Private schema As New Hashtable 'schema for currently connected db
    Private conn As DbConnection 'actual db connection - SqlConnection or OleDbConnection

    Private is_check_ole_types As Boolean = False 'if true - checks for unsupported OLE types during readRow
    Private ReadOnly UNSUPPORTED_OLE_TYPES As New Hashtable

    ''' <summary>
    ''' "synax sugar" helper to build Hashtable from list of arguments instead more complex New Hashtable from {...}
    ''' Example: db.row("table", h("id", 123)) => "select * from table where id=123"
    ''' </summary>
    ''' <param name="args">even number of args required</param>
    ''' <returns></returns>
    Public Shared Function h(ParamArray args() As Object) As Hashtable
        If args.Length = 0 OrElse args.Length Mod 2 <> 0 Then Throw New ArgumentException("h() accepts even number of arguments")
        Dim result As New Hashtable
        For i = 0 To args.Length - 1 Step 2
            result(args(i)) = args(i + 1)
        Next
        Return result
    End Function

    ''' <summary>
    ''' construct new DB object with
    ''' </summary>
    ''' <param name="fw">framework reference</param>
    ''' <param name="conf">config hashtable with "connection_string" and "type" keys. If none - fw.config("db")("main") used</param>
    ''' <param name="db_name">database human name, only used for logger</param>
    Public Sub New(fw As FW, Optional conf As Hashtable = Nothing, Optional db_name As String = "main")
        Me.fw = fw
        If conf IsNot Nothing Then
            Me.conf = conf
        Else
            Me.conf = fw.config("db")("main")
        End If
        Me.dbtype = Me.conf("type")
        Me.connstr = Me.conf("connection_string")

        Me.db_name = db_name

        Me.UNSUPPORTED_OLE_TYPES = Utils.qh("DBTYPE_IDISPATCH DBTYPE_IUNKNOWN") 'also? DBTYPE_ARRAY DBTYPE_VECTOR DBTYPE_BYTES
    End Sub

    Public Sub logger(level As LogLevel, ByVal ParamArray args() As Object)
        If args.Length = 0 Then Return
        fw.logger(level, args)
    End Sub

    ''' <summary>
    ''' connect to DB server using connection string defined in web.config appSettings, key db|main|connection_string (by default)
    ''' </summary>
    ''' <returns></returns>
    Public Function connect() As DbConnection
        Dim cache_key = "DB#" & connstr

        'first, try to get connection from request cache (so we will use only one connection per db server - TBD make configurable?)
        If conn Is Nothing Then
            conn = fw.cache.getRequestValue(cache_key)
        End If

        'if still no connection - re-make it
        If conn Is Nothing Then
            schema = New Hashtable 'reset schema cache
            conn = createConnection(connstr, conf("type"))
            fw.cache.setRequestValue(cache_key, conn)
        End If

        'if it's disconnected - re-connect
        If conn.State <> ConnectionState.Open Then
            conn.Open()
        End If

        If Me.dbtype = "OLE" Then
            is_check_ole_types = True
        Else
            is_check_ole_types = False
        End If

        Return conn
    End Function

    Public Sub disconnect()
        If Me.conn IsNot Nothing Then Me.conn.Close()
    End Sub

    ''' <summary>
    ''' return internal connection object
    ''' </summary>
    ''' <returns></returns>
    Public Function getConnection() As DbConnection
        Return conn
    End Function

    Public Function createConnection(connstr As String, Optional dbtype As String = "SQL") As DbConnection
        Dim result As DbConnection

        If dbtype = "SQL" Then
            result = New SqlConnection(connstr)
        ElseIf dbtype = "OLE" Then
            result = New OleDbConnection(connstr)
        ElseIf dbtype = "ODBC" Then
            result = New OdbcConnection(connstr)
        Else
            Dim msg As String = "Unknown type [" & dbtype & "]"
            logger(LogLevel.FATAL, msg)
            Throw New ApplicationException(msg)
        End If

        result.Open()
        Return result
    End Function

    Public Sub check_create_mdb(filepath As String)
        If File.Exists(filepath) Then Exit Sub

        Dim connstr As String = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" & filepath

        Dim cat As Object = CreateObject("ADOX.Catalog")
        cat.Create(connstr)
        cat.ActiveConnection.Close()
    End Sub

    <Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")>
    Public Function query(ByVal sql As String) As DbDataReader
        connect()
        logger(LogLevel.INFO, "DB:", db_name, " ", sql)

        SQL_QUERY_CTR += 1

        Dim dbcomm As DbCommand = Nothing
        If dbtype = "SQL" Then
            dbcomm = New SqlCommand(sql, conn)
        ElseIf dbtype = "OLE" Then
            dbcomm = New OleDbCommand(sql, conn)
        End If

        Dim dbread As DbDataReader = dbcomm.ExecuteReader()
        Return dbread
    End Function

    'exectute without results (so db reader will be closed), return number of rows affected.
    <Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")>
    Public Function exec(ByVal sql As String) As Integer
        connect()
        logger(LogLevel.INFO, "DB:", db_name, ", SQL QUERY: ", sql)

        SQL_QUERY_CTR += 1

        Dim dbcomm As DbCommand = Nothing
        If dbtype = "SQL" Then
            dbcomm = New SqlCommand(sql, conn)
        ElseIf dbtype = "OLE" Then
            dbcomm = New OleDbCommand(sql, conn)
        End If

        Return dbcomm.ExecuteNonQuery()
    End Function

    Private Function readRow(dbread As DbDataReader) As Hashtable
        Dim result As New Hashtable

        For i As Integer = 0 To dbread.FieldCount - 1
            Try
                If is_check_ole_types AndAlso UNSUPPORTED_OLE_TYPES.ContainsKey(dbread.GetDataTypeName(i)) Then Continue For

                Dim value As String = dbread(i).ToString()
                Dim name As String = dbread.GetName(i).ToString()
                result.Add(name, value)
            Catch Ex As Exception
                Exit For
            End Try
        Next i

        Return result
    End Function

    Public Overloads Function row(ByVal sql As String) As Hashtable
        Dim dbread As DbDataReader = query(sql)
        dbread.Read()

        Dim h As New Hashtable
        If dbread.HasRows Then
            h = readRow(dbread)
        End If

        dbread.Close()
        Return h
    End Function

    Public Overloads Function row(ByVal table As String, ByVal where As Hashtable, Optional order_by As String = "") As Hashtable
        Return row(hash2sql_select(table, where, order_by))
    End Function

    Public Overloads Function array(ByVal sql As String) As ArrayList
        Dim dbread As DbDataReader = query(sql)
        Dim a As New ArrayList

        While dbread.Read()
            a.Add(readRow(dbread))
        End While

        dbread.Close()
        Return a
    End Function

    ''' <summary>
    ''' return all rows with all fields from the table based on coditions/order
    ''' array("table", where, "id asc", Utils.qh("field1|id field2|iname"))
    ''' </summary>
    ''' <param name="table">table name</param>
    ''' <param name="where">where conditions</param>
    ''' <param name="order_by">optional order by, MUST BE QUOTED</param>
    ''' <param name="aselect_fields">optional select fields array or hashtable(for aliases) or arraylist of hashtable("field"=>,"alias"=> for cases if there could be several same fields with diff aliases), if not set * returned</param>
    ''' <returns></returns>
    Public Overloads Function array(ByVal table As String, ByVal where As Hashtable, Optional ByRef order_by As String = "", Optional aselect_fields As ICollection = Nothing) As ArrayList
        Dim select_fields = "*"
        If aselect_fields IsNot Nothing Then
            Dim quoted As New ArrayList
            If TypeOf aselect_fields Is ArrayList Then
                'arraylist of hashtables with "field","alias" keys - usable for the case when we need same field to be selected more than once with different aliases
                For Each asf As Hashtable In aselect_fields
                    quoted.Add(Me.q_ident(asf("field")) & " as " & Me.q_ident(asf("alias")))
                Next

            ElseIf TypeOf aselect_fields Is IDictionary Then
                For Each field In DirectCast(aselect_fields, IDictionary).Keys
                    quoted.Add(Me.q_ident(field) & " as " & Me.q_ident(DirectCast(aselect_fields, IDictionary).Item(field))) 'field as alias
                Next
            Else 'IList
                For Each field As String In aselect_fields
                    quoted.Add(Me.q_ident(field))
                Next
            End If
            select_fields = IIf(quoted.Count > 0, Join(quoted.ToArray(), ", "), "*")
        End If

        Return array(hash2sql_select(table, where, order_by, select_fields))
    End Function

    'return just first column values as arraylist
    Public Overloads Function col(ByVal sql As String) As ArrayList
        Dim dbread As DbDataReader = query(sql)
        Dim a As New ArrayList
        While dbread.Read()
            a.Add(dbread(0).ToString())
        End While

        dbread.Close()
        Return a
    End Function

    ''' <summary>
    ''' return just one column values as arraylist
    ''' </summary>
    ''' <param name="table">table name</param>
    ''' <param name="where">where conditions</param>
    ''' <param name="field_name">optional field name, if empty - first field returned</param>
    ''' <param name="order_by">optional order by (MUST be quoted)</param>
    ''' <returns></returns>
    Public Overloads Function col(table As String, where As Hashtable, Optional field_name As String = "", Optional ByRef order_by As String = "") As ArrayList
        If String.IsNullOrEmpty(field_name) Then
            field_name = "*"
        Else
            field_name = q_ident(field_name)
        End If
        Return col(hash2sql_select(table, where, order_by, field_name))
    End Function

    'return just first value from column
    Public Overloads Function value(ByVal sql As String) As Object
        Dim dbread As DbDataReader = query(sql)
        Dim result As Object = Nothing

        While dbread.Read()
            result = dbread(0)
            Exit While 'just return first row
        End While

        dbread.Close()
        Return result
    End Function

    ''' <summary>
    ''' Return just one field value:
    ''' value("table", where)
    ''' value("table", where, "field1")
    ''' value("table", where, "1") 'just return 1, useful for exists queries
    ''' value("table", where, "count(*)", "id asc")
    ''' </summary>
    ''' <param name="table"></param>
    ''' <param name="where"></param>
    ''' <param name="field_name">field name, special cases: "1", "count(*)"</param>
    ''' <param name="order_by"></param>
    ''' <returns></returns>
    Public Overloads Function value(table As String, where As Hashtable, Optional field_name As String = "", Optional ByRef order_by As String = "") As Object
        If String.IsNullOrEmpty(field_name) Then
            field_name = "*"
        ElseIf field_name = "count(*)" OrElse field_name = "1" Then
            'no changes
        Else
            field_name = q_ident(field_name)
        End If
        Return value(hash2sql_select(table, where, order_by, field_name))
    End Function

    'string will be Left(RTrim(str),length)
    Public Function left(str As String, length As Integer) As String
        If String.IsNullOrEmpty(str) Then Return ""
        Return RTrim(str).Substring(0, length)
    End Function

    'create "IN (1,2,3)" sql or IN (NULL) if empty params passed
    'examples:
    ' where = " field "& db.insql("a,b,c,d")
    ' where = " field "& db.insql(string())
    ' where = " field "& db.insql(ArrayList)
    Public Function insql(params As String) As String
        Return insql(Split(params, ","))
    End Function
    Public Function insql(params As IList) As String
        Dim result As New ArrayList
        For Each param As String In params
            result.Add(Me.q(param))
        Next
        Return " IN (" & IIf(result.Count > 0, Join(result.ToArray(), ", "), "NULL") & ")"
    End Function
    'same as insql, but for quoting numbers - uses qi() 
    Public Function insqli(params As String) As String
        Return insqli(Split(params, ","))
    End Function
    Public Function insqli(params As IList) As String
        Dim result As New ArrayList
        For Each param As String In params
            result.Add(Me.qi(param))
        Next
        Return " IN (" & IIf(result.Count > 0, Join(result.ToArray(), ", "), "NULL") & ")"
    End Function

    'quote identifier: table => [table]
    Public Function q_ident(ByVal str As String) As String
        If IsNothing(str) Then str = ""
        str = Replace(str, "[", "")
        str = Replace(str, "]", "")
        Return "[" & str & "]"
    End Function

    'if length defined - string will be Left(Trim(str),length) before quoted
    Public Function q(ByVal str As String, Optional length As Integer = 0) As String
        If IsNothing(str) Then str = ""
        If length > 0 Then str = Me.left(str, length)
        Return "'" & Replace(str, "'", "''") & "'"
    End Function

    'simple just replace quotes, don't add start/end single quote - for example, for use with LIKE
    Public Function qq(ByVal str As String) As String
        If IsNothing(str) Then str = ""
        Return Replace(str, "'", "''")
    End Function

    'simple quote as Integer Value
    Public Function qi(ByVal str As String) As Integer
        Return Utils.f2int(str)
    End Function

    'simple quote as Float Value
    Public Function qf(ByVal str As String) As Double
        Return Utils.f2float(str)
    End Function

    'simple quote as Date Value
    Public Function qd(ByVal str As String) As String
        Dim result As String
        If dbtype = "SQL" Then
            Dim tmpdate As DateTime
            If DateTime.TryParse(str, tmpdate) Then
                result = "convert(DATETIME2, '" & tmpdate.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.DateTimeFormatInfo.InvariantInfo) & "', 120)"
            Else
                result = "NULL"
            End If
        Else
            result = Regex.Replace(str.ToString, "['""\]\[]", "")
            If Regex.IsMatch(result, "\D") Then
                result = "'" & str & "'"
            Else
                result = "NULL"
            End If
        End If
        Return result
    End Function

    Public Function quote(ByVal table As String, ByVal fields As Hashtable) As Hashtable
        connect()
        load_table_schema(table)
        If Not schema.ContainsKey(table) Then Throw New ApplicationException("table [" & table & "] does not defined in FW.config(""schema"")")

        Dim fieldsq As New Hashtable
        Dim k As String

        For Each k In fields.Keys
            Dim q = qone(table, k, fields(k))
            'quote field name too
            If q IsNot Nothing Then fieldsq(q_ident(k)) = q
        Next k

        Return fieldsq
    End Function

    'can return String or DBOperation class
    Public Function qone(ByVal table As String, ByVal field_name As String, ByVal field_value_or_op As Object) As Object
        connect()
        load_table_schema(table)
        field_name = field_name.ToLower()
        If Not schema(table).containskey(field_name) Then Throw New ApplicationException("field " & table & "." & field_name & " does not defined in FW.config(""schema"") ")

        Dim field_value As Object
        Dim dbop As DBOperation = Nothing
        If TypeOf field_value_or_op Is DBOperation Then
            dbop = DirectCast(field_value_or_op, DBOperation)
            field_value = dbop.value
        Else
            field_value = field_value_or_op
        End If

        Dim field_type As String = schema(table)(field_name)
        Dim quoted As String
        If dbop IsNot Nothing Then
            If dbop.op = DBOps.IN OrElse dbop.op = DBOps.NOTIN Then
                If dbop.value IsNot Nothing AndAlso TypeOf (dbop.value) Is IList Then
                    Dim result As New ArrayList
                    For Each param In dbop.value
                        result.Add(qone_by_type(field_type, param))
                    Next
                    quoted = "(" & IIf(result.Count > 0, Join(result.ToArray(), ", "), "NULL") & ")"
                Else
                    quoted = qone_by_type(field_type, field_value)
                End If
            ElseIf dbop.op = DBOps.BETWEEN Then
                quoted = qone_by_type(field_type, dbop.value(0)) & " AND " & qone_by_type(field_type, dbop.value(1))
            Else
                quoted = qone_by_type(field_type, field_value)
            End If
        Else
            quoted = qone_by_type(field_type, field_value)
        End If

        If dbop IsNot Nothing Then
            dbop.quoted_value = quoted
            Return field_value_or_op
        Else
            Return quoted
        End If
    End Function

    Function qone_by_type(field_type As String, field_value As Object) As String
        Dim quoted As String

        'if value set to Nothing or DBNull - assume it's NULL in db
        If field_value Is Nothing OrElse IsDBNull(field_value) Then
            quoted = "NULL"
        Else
            'fw.logger(table & "." & field_name & " => " & field_type & ", value=[" & field_value & "]")
            If Regex.IsMatch(field_type, "int") Then
                If field_value IsNot Nothing AndAlso Regex.IsMatch(field_value, "true", RegexOptions.IgnoreCase) Then
                    quoted = "1"
                ElseIf field_value IsNot Nothing AndAlso Regex.IsMatch(field_value, "false", RegexOptions.IgnoreCase) Then
                    quoted = "0"
                ElseIf field_value IsNot Nothing AndAlso TypeOf field_value Is String AndAlso field_value = "" Then
                    'if empty string for numerical field - assume NULL
                    quoted = "NULL"
                Else
                    quoted = Utils.f2int(field_value)
                End If

            ElseIf field_type = "datetime" Then
                quoted = Me.qd(field_value)

            ElseIf field_type = "float" Then
                quoted = Utils.f2float(field_value)

            Else
                'fieldsq(k) = "'" & Regex.Replace(fields(k), "(['""])", "\\$1") & "'"
                If IsNothing(field_value) Then
                    quoted = "''"
                Else
                    'escape backslash following by carriage return char(13) with doubling backslash and carriage return
                    'because of https://msdn.microsoft.com/en-us/library/dd207007.aspx
                    quoted = Regex.Replace(field_value, "\\(\r\n?)", "\\$1$1")
                    quoted = Regex.Replace(quoted, "'", "''") 'escape single quotes
                    quoted = "'" & quoted & "'"
                End If
            End If
        End If
        Return quoted
    End Function

    'operations support for non-raw sql methods

    ''' <summary>
    ''' NOT EQUAL operation 
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"status", db.opNOT(127)}})
    ''' <![CDATA[ select * from users where status<>127 ]]>
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Function opNOT(value As Object) As DBOperation
        Return New DBOperation(DBOps.NOT, value)
    End Function

    ''' <summary>
    ''' LESS or EQUAL than operation
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opLE(50)}})
    ''' <![CDATA[ select * from users where access_level<=50 ]]>
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Function opLE(value As Object) As DBOperation
        Return New DBOperation(DBOps.LE, value)
    End Function

    ''' <summary>
    ''' LESS THAN operation
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opLT(50)}})
    ''' <![CDATA[ select * from users where access_level<50 ]]>
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Function opLT(value As Object) As DBOperation
        Return New DBOperation(DBOps.LT, value)
    End Function

    ''' <summary>
    ''' GREATER or EQUAL than operation
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGE(50)}})
    ''' <![CDATA[ select * from users where access_level>=50 ]]>
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Function opGE(value As Object) As DBOperation
        Return New DBOperation(DBOps.GE, value)
    End Function

    ''' <summary>
    ''' GREATER THAN operation
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGT(50)}})
    ''' <![CDATA[ select * from users where access_level>50 ]]>
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Function opGT(value As Object) As DBOperation
        Return New DBOperation(DBOps.GT, value)
    End Function

    ''' <summary>
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opISNULL()}})
    ''' select * from users where field IS NULL
    ''' </summary>
    ''' <returns></returns>
    Public Function opISNULL() As DBOperation
        Return New DBOperation(DBOps.ISNULL)
    End Function
    ''' <summary>
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opISNOTNULL()}})
    ''' select * from users where field IS NOT NULL
    ''' </summary>
    ''' <returns></returns>
    Public Function opISNOTNULL() As DBOperation
        Return New DBOperation(DBOps.ISNOTNULL)
    End Function
    ''' <summary>
    ''' Example: Dim rows = DB.array("users", New Hashtable From {{"address1", db.opLIKE("%Orlean%")}})
    ''' select * from users where address1 LIKE '%Orlean%'
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Function opLIKE(value As Object) As DBOperation
        Return New DBOperation(DBOps.LIKE, value)
    End Function
    ''' <summary>
    ''' Example: Dim rows = DB.array("users", New Hashtable From {{"address1", db.opNOTLIKE("%Orlean%")}})
    ''' select * from users where address1 NOT LIKE '%Orlean%'
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Function opNOTLIKE(value As Object) As DBOperation
        Return New DBOperation(DBOps.NOTLIKE, value)
    End Function

    ''' <summary>
    ''' 2 ways to call:
    ''' opIN(1,2,4) - as multiple arguments
    ''' opIN(array) - as one array of values
    ''' 
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"id", db.opIN(1, 2)}})
    ''' select * from users where id IN (1,2)
    ''' </summary>
    ''' <param name="args"></param>
    ''' <returns></returns>
    Public Function opIN(ParamArray args() As Object) As DBOperation
        Dim values As Object
        If args.Count = 1 AndAlso (IsArray(args(0)) OrElse TypeOf (args(0)) Is IList) Then
            values = args(0)
        Else
            values = args
        End If
        Return New DBOperation(DBOps.IN, values)
    End Function

    ''' <summary>
    ''' 2 ways to call:
    ''' opIN(1,2,4) - as multiple arguments
    ''' opIN(array) - as one array of values
    ''' 
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"id", db.opNOTIN(1, 2)}})
    ''' select * from users where id NOT IN (1,2)
    ''' </summary>
    ''' <param name="args"></param>
    ''' <returns></returns>
    Public Function opNOTIN(ParamArray args() As Object) As DBOperation
        Dim values As Object
        If args.Count = 1 AndAlso (IsArray(args(0)) OrElse TypeOf (args(0)) Is IList) Then
            values = args(0)
        Else
            values = args
        End If
        Return New DBOperation(DBOps.NOTIN, values)
    End Function

    ''' <summary>
    ''' Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opBETWEEN(10,20)}})
    ''' select * from users where field BETWEEN 10 AND 20
    ''' </summary>
    ''' <returns></returns>
    Public Function opBETWEEN(from_value As Object, to_value As Object) As DBOperation
        Return New DBOperation(DBOps.BETWEEN, New Object() {from_value, to_value})
    End Function

    'return last inserted id
    Public Function insert(ByVal table As String, ByVal fields As Hashtable) As Integer
        If fields.Count < 1 Then Return False
        exec(hash2sql_i(table, fields))

        Dim insert_id As Object

        If dbtype = "SQL" Then
            insert_id = value("SELECT SCOPE_IDENTITY() AS [SCOPE_IDENTITY] ")
        ElseIf dbtype = "OLE" Then
            insert_id = value("SELECT @@identity")
        Else
            Throw New ApplicationException("Get last insert ID for DB type [" & dbtype & "] not implemented")
        End If

        'if table doesn't have identity insert_id would be DBNull
        If IsDBNull(insert_id) Then insert_id = 0

        Return insert_id
    End Function

    Public Overloads Function update(ByVal sql As String) As Integer
        Return exec(sql)
    End Function

    Public Overloads Function update(ByVal table As String, ByVal fields As Hashtable, ByVal where As Hashtable) As Integer
        Return exec(hash2sql_u(table, fields, where))
    End Function

    'retrun number of affected rows
    Public Function update_or_insert(ByVal table As String, ByVal fields As Hashtable, ByVal where As Hashtable) As Integer
        ' merge fields and where
        Dim allfields As New Hashtable
        Dim k As String
        For Each k In fields.Keys
            allfields(k) = fields(k)
        Next k

        For Each k In where.Keys
            allfields(k) = where(k)
        Next k

        Dim update_sql As String = hash2sql_u(table, fields, where)
        Dim insert_sql As String = hash2sql_i(table, allfields)
        Dim full_sql As String = update_sql & "  IF @@ROWCOUNT = 0 " & insert_sql

        Return exec(full_sql)
    End Function

    'retrun number of affected rows
    Public Function del(ByVal table As String, ByVal where As Hashtable) As Integer
        Return exec(hash2sql_d(table, where))
    End Function

    'join key/values with quoting values according to table
    ' h - already quoted! values
    ' kv_delim = pass "" to autodetect " = " or " IS " (for NULL values)
    Public Function _join_hash(h As Hashtable, ByVal kv_delim As String, ByVal pairs_delim As String) As String
        Dim res As String = ""
        If h.Count < 1 Then Return res

        Dim ar(h.Count - 1) As String

        Dim i As Integer = 0
        Dim k As String
        For Each k In h.Keys
            Dim vv = h(k)
            Dim v = ""
            Dim delim = kv_delim
            If String.IsNullOrEmpty(delim) Then
                If TypeOf vv Is DBOperation Then
                    Dim dbop = DirectCast(vv, DBOperation)
                    delim = " " & dbop.opstr & " "
                    If dbop.is_value Then
                        v = dbop.quoted_value
                    End If
                Else
                    v = vv
                    If vv = "NULL" Then
                        delim = " IS "
                    Else
                        delim = "="
                    End If
                End If
            Else
                v = vv
            End If
            ar(i) = k & delim & v
            i += 1
        Next k
        res = String.Join(pairs_delim, ar)
        Return res
    End Function

    ''' <summary>
    ''' build SELECT sql string
    ''' </summary>
    ''' <param name="table">table name</param>
    ''' <param name="where">where conditions</param>
    ''' <param name="order_by">optional order by string</param>
    ''' <param name="select_fields">MUST already be quoted!</param>
    ''' <returns></returns>
    Private Function hash2sql_select(ByVal table As String, ByVal where As Hashtable, Optional ByRef order_by As String = "", Optional select_fields As String = "*") As String
        where = quote(table, where)
        'FW.logger(where)
        Dim where_string As String = _join_hash(where, "", " AND ")
        If where_string.Length > 0 Then where_string = " WHERE " & where_string

        Dim sql As String = "SELECT " & select_fields & " FROM " & q_ident(table) & " " & where_string
        If order_by.Length > 0 Then sql = sql & " ORDER BY " & order_by
        Return sql
    End Function

    Public Function hash2sql_u(ByVal table As String, ByVal fields As Hashtable, ByVal where As Hashtable) As String
        fields = quote(table, fields)
        where = quote(table, where)

        Dim update_string As String = _join_hash(fields, "=", ", ")
        Dim where_string As String = _join_hash(where, "", " AND ")

        If where_string.Length > 0 Then where_string = " WHERE " & where_string

        Dim sql As String = "UPDATE " & q_ident(table) & " " & " SET " & update_string & where_string

        Return sql
    End Function

    Private Function hash2sql_i(ByVal table As String, ByVal fields As Hashtable) As String
        fields = quote(table, fields)

        Dim ar(fields.Count - 1) As String

        fields.Keys.CopyTo(ar, 0)
        Dim names_string As String = String.Join(", ", ar)

        fields.Values.CopyTo(ar, 0)
        Dim values_string As String = String.Join(", ", ar)
        Dim sql As String = "INSERT INTO " & q_ident(table) & " (" & names_string & ") VALUES (" & values_string & ")"
        Return sql
    End Function

    Private Function hash2sql_d(ByVal table As String, ByVal where As Hashtable) As String
        where = quote(table, where)
        Dim where_string As String = _join_hash(where, "", " AND ")
        If where_string.Length > 0 Then where_string = " WHERE " & where_string

        Dim sql As String = "DELETE FROM " & q_ident(table) & " " & where_string
        Return sql
    End Function

    'return array of table names in current db
    Public Function tables() As ArrayList
        Dim result As New ArrayList

        Dim conn As DbConnection = Me.connect()
        Dim dataTable As DataTable = conn.GetSchema("Tables")
        For Each row As DataRow In dataTable.Rows
            'fw.logger("************ TABLE" & row("TABLE_NAME"))
            'For Each cl As DataColumn In dataTable.Columns
            '    fw.logger(cl.ToString & " = " & row(cl))
            'Next

            'skip any system tables or views (VIEW, ACCESS TABLE, SYSTEM TABLE)
            If row("TABLE_TYPE") <> "TABLE" AndAlso row("TABLE_TYPE") <> "BASE TABLE" AndAlso row("TABLE_TYPE") <> "PASS-THROUGH" Then Continue For
            Dim tblname As String = row("TABLE_NAME").ToString()
            result.Add(tblname)
        Next

        Return result
    End Function

    'return array of view names in current db
    Public Function views() As ArrayList
        Dim result As New ArrayList

        Dim conn As DbConnection = Me.connect()
        Dim dataTable As DataTable = conn.GetSchema("Tables")
        For Each row As DataRow In dataTable.Rows
            'skip non-views
            If row("TABLE_TYPE") <> "VIEW" Then Continue For
            Dim tblname As String = row("TABLE_NAME").ToString()
            result.Add(tblname)
        Next

        Return result
    End Function

    Public Function load_table_schema_full(table As String) As ArrayList
        'check if full schema already there
        If IsNothing(schemafull_cache) Then schemafull_cache = New Hashtable
        If Not schemafull_cache.ContainsKey(connstr) Then schemafull_cache(connstr) = New Hashtable
        If schemafull_cache(connstr).ContainsKey(table) Then
            Return schemafull_cache(connstr)(table)
        End If

        'cache miss
        Dim result As New ArrayList
        If dbtype = "SQL" Then
            'fw.logger("cache MISS " & current_db & "." & table)
            'get information about all columns in the table
            'default = ((0)) ('') (getdate())
            'maxlen = -1 for nvarchar(MAX)
            Dim sql As String = "SELECT c.column_name as 'name'," &
                    " c.data_type as 'type'," &
                    " CASE c.is_nullable WHEN 'YES' THEN 1 ELSE 0 END AS 'is_nullable'," &
                    " c.column_default as 'default'," &
                    " c.character_maximum_length as 'maxlen'," &
                    " c.numeric_precision," &
                    " c.numeric_scale," &
                    " c.character_set_name as 'charset'," &
                    " c.collation_name as 'collation'," &
                    " c.ORDINAL_POSITION as 'pos'," &
                    " COLUMNPROPERTY(object_id(c.table_name), c.column_name, 'IsIdentity') as is_identity" &
                    " FROM INFORMATION_SCHEMA.TABLES t," &
                    "   INFORMATION_SCHEMA.COLUMNS c" &
                    " WHERE t.table_name = c.table_name" &
                    "   AND t.table_name = " & q(table) &
                    " order by c.ORDINAL_POSITION"
            result = array(sql)
            For Each row As Hashtable In result
                row("fw_type") = map_mssqltype2fwtype(row("type")) 'meta type
                row("fw_subtype") = LCase(row("type"))
            Next
            'TODO else ODBC support
        Else
            'OLE DB (Access)
            Dim schemaTable As DataTable =
                DirectCast(conn, OleDbConnection).GetOleDbSchemaTable(OleDb.OleDbSchemaGuid.Columns, New Object() {Nothing, Nothing, table, Nothing})

            Dim fieldslist = New List(Of Hashtable)
            For Each row As DataRow In schemaTable.Rows
                'unused:
                'COLUMN_HASDEFAULT True False
                'COLUMN_FLAGS   74 86 90(auto) 102 106 114 122(date) 130 226 230 234
                'CHARACTER_OCTET_LENGTH
                'DATETIME_PRECISION=0
                'DESCRIPTION
                Dim h = New Hashtable
                h("name") = row("COLUMN_NAME").ToString()
                h("type") = row("DATA_TYPE")
                h("fw_type") = map_oletype2fwtype(row("DATA_TYPE")) 'meta type
                h("fw_subtype") = LCase([Enum].GetName(GetType(OleDbType), row("DATA_TYPE"))) 'exact type as string
                h("is_nullable") = IIf(row("IS_NULLABLE"), 1, 0)
                h("default") = row("COLUMN_DEFAULT") '"=Now()" "0" "No"
                h("maxlen") = row("CHARACTER_MAXIMUM_LENGTH")
                h("numeric_precision") = row("NUMERIC_PRECISION")
                h("numeric_scale") = row("NUMERIC_SCALE")
                h("charset") = row("CHARACTER_SET_NAME")
                h("collation") = row("COLLATION_NAME")
                h("pos") = row("ORDINAL_POSITION")
                h("is_identity") = 0
                h("desc") = row("DESCRIPTION")
                h("column_flags") = row("COLUMN_FLAGS")
                fieldslist.Add(h)
            Next
            'order by ORDINAL_POSITION
            result.AddRange(fieldslist.OrderBy(Function(h) h("pos")).ToList())

            'now detect identity (because order is important)
            For Each h As Hashtable In result
                'actually this also triggers for Long Integers, so for now - only first field that match conditions will be an identity
                If h("type") = OleDbType.Integer AndAlso h("column_flags") = 90 Then
                    h("is_identity") = 1
                    Exit For
                End If
            Next
        End If

        'save to cache
        schemafull_cache(connstr)(table) = result

        Return result
    End Function

    'return database foreign keys, optionally filtered by table (that contains foreign keys)
    Public Function get_foreign_keys(Optional table As String = "") As ArrayList
        Dim result As New ArrayList
        If dbtype = "SQL" Then
            Dim where = ""
            If table > "" Then where = " WHERE col1.TABLE_NAME=" & Me.q(table)
            result = Me.array("SELECT " &
                 " col1.CONSTRAINT_NAME as [name]" &
                 ", col1.TABLE_NAME As [table]" &
                 ", col1.COLUMN_NAME as [column]" &
                 ", col2.TABLE_NAME as [pk_table]" &
                 ", col2.COLUMN_NAME as [pk_column]" &
                 ", rc.UPDATE_RULE as [on_update]" &
                 ", rc.DELETE_RULE as [on_delete]" &
                 " FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc " &
                 " INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col1 " &
                 "   ON (col1.CONSTRAINT_CATALOG = rc.CONSTRAINT_CATALOG  " &
                 "       AND col1.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA " &
                 "       AND col1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME)" &
                 " INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col2 " &
                 "   ON (col2.CONSTRAINT_CATALOG = rc.UNIQUE_CONSTRAINT_CATALOG  " &
                 "       AND col2.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA " &
                 "       AND col2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME " &
                 "       AND col2.ORDINAL_POSITION = col1.ORDINAL_POSITION)" &
                 where)
            'on_update or on_delete can contain: NO ACTION, CASCASE

        Else
            Dim dt = DirectCast(conn, OleDbConnection).GetOleDbSchemaTable(OleDb.OleDbSchemaGuid.Foreign_Keys, New Object() {Nothing})
            For Each row As DataRow In dt.Rows
                If table > "" AndAlso row("FK_TABLE_NAME") <> table Then Continue For

                result.Add(New Hashtable From {
                            {"table", row("FK_TABLE_NAME")},
                            {"column", row("FK_COLUMN_NAME")},
                            {"name", row("FK_NAME")},
                            {"pk_table", row("PK_TABLE_NAME")},
                            {"pk_column", row("PK_COLUMN_NAME")},
                            {"on_update", row("UPDATE_RULE")},
                            {"on_delete", row("DELETE_RULE")}
                       })
                'on_update or on_delete can contain: NO ACTION, CASCASE
            Next
        End If

        Return result
    End Function

    'load table schema from db
    Public Function load_table_schema(table As String) As Hashtable
        'for non-MSSQL schemas - just use config schema for now - TODO
        If dbtype <> "SQL" AndAlso dbtype <> "OLE" Then
            If schema.Count = 0 Then
                schema = conf("schema")
            End If
            Return Nothing
        End If

        'check if schema already there
        If schema.ContainsKey(table) Then Return schema(table)

        If IsNothing(schema_cache) Then schema_cache = New Hashtable
        If Not schema_cache.ContainsKey(connstr) Then schema_cache(connstr) = New Hashtable
        If Not schema_cache(connstr).ContainsKey(table) Then
            Dim h As New Hashtable

            Dim fields As ArrayList = load_table_schema_full(table)
            For Each row As Hashtable In fields
                h(row("name").ToString().ToLower()) = row("fw_type")
            Next

            schema(table) = h
            schema_cache(connstr)(table) = h
        Else
            'fw.logger("schema_cache HIT " & current_db & "." & table)
            schema(table) = schema_cache(connstr)(table)
        End If

        Return schema(table)
    End Function

    Public Sub clear_schema_cache()
        If schemafull_cache IsNot Nothing Then schemafull_cache.Clear()
        If schema_cache IsNot Nothing Then schema_cache.Clear()
        If schema IsNot Nothing Then schema.Clear()
    End Sub

    Private Function map_mssqltype2fwtype(mstype As String) As String
        Dim result As String
        Select Case LCase(mstype)
            'TODO - unsupported: image, varbinary, timestamp
            Case "tinyint", "smallint", "int", "bigint", "bit"
                result = "int"
            Case "real", "numeric", "decimal", "money", "smallmoney", "float"
                result = "float"
            Case "datetime", "datetime2", "date", "smalldatetime"
                result = "datetime"
            Case Else '"text", "ntext", "varchar", "nvarchar", "char", "nchar"
                result = "varchar"
        End Select

        Return result
    End Function

    Private Function map_oletype2fwtype(mstype As Integer) As String
        Dim result As String
        Select Case mstype
            'TODO - unsupported: image, varbinary, longvarbinary, dbtime, timestamp
            'NOTE: Boolean here is: True=-1 (vbTrue), False=0 (vbFalse)
            Case OleDbType.Boolean, OleDbType.TinyInt, OleDbType.UnsignedTinyInt, OleDbType.SmallInt, OleDbType.UnsignedSmallInt, OleDbType.Integer, OleDbType.UnsignedInt, OleDbType.BigInt, OleDbType.UnsignedBigInt
                result = "int"
            Case OleDbType.Double, OleDbType.Numeric, OleDbType.VarNumeric, OleDbType.Single, OleDbType.Decimal, OleDbType.Currency
                result = "float"
            Case OleDbType.Date, OleDbType.DBDate, OleDbType.DBTimeStamp
                result = "datetime"
            Case Else '"text", "ntext", "varchar", "longvarchar" "nvarchar", "char", "nchar", "wchar", "varwchar", "longvarwchar", "dbtime"
                result = "varchar"
        End Select

        Return result
    End Function

#Region "IDisposable Support"
    Private disposedValue As Boolean ' To detect redundant calls

    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                Me.disconnect()
            End If
        End If
        disposedValue = True
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
    End Sub
#End Region

End Class
