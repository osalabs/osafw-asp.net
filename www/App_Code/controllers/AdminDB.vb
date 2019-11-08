' Direct DB Access Controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.Data.Common
Imports System.Data

Public Class AdminDBController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Private Const dbpwd As String = "db321"

    Public Function IndexAction() As Hashtable
        Dim ps As New Hashtable
        Dim selected_db = reqs("db")
        If selected_db = "" Then selected_db = "main"

        Dim sql As String = reqs("sql")
        Dim tablehead As ArrayList = Nothing
        Dim tablerows As ArrayList = Nothing
        Dim sql_ctr As Integer = 0
        Dim sql_time As Long = DateTime.Now().Ticks

        Try
            If selected_db > "" Then
                logger("CONNECT TO", selected_db)
                db = New DB(fw, fw.config("db")(selected_db), selected_db)
            End If

            If fw.SESSION("admindb_pwd_checked") Or reqs("pwd") = dbpwd Then
                fw.SESSION("admindb_pwd_checked", True)
            Else
                If sql > "" Then fw.G("err_msg") = "Wrong password"
            End If
            If sql > "" AndAlso fw.SESSION("admindb_pwd_checked") Then
                If sql = "show tables" Then
                    'special case - show tables
                    show_tables(tablehead, tablerows)
                Else
                    'launch the query
                    Dim sql1 As String = strip_comments(sql)
                    Dim asql() As [String] = split_multi_sql(sql)
                    For Each sqlone As String In asql
                        sqlone = Trim(sqlone)
                        If sqlone > "" Then
                            Dim sth As DbDataReader = db.query(sqlone)
                            tablehead = sth2head(sth)
                            tablerows = sth2table(sth)
                            sth.Close()
                            sql_ctr += 1
                        End If
                    Next
                End If
            End If
        Catch ex As Exception
            fw.G("err_msg") = "Error occured: " & ex.Message
        End Try

        Dim dbsources As New ArrayList
        For Each dbname As String In fw.config("db").Keys
            dbsources.Add(New Hashtable From {
                            {"id", dbname},
                            {"iname", dbname},
                            {"is_checked", dbname = selected_db}
                          })
        Next

        ps("dbsources") = dbsources
        ps("selected_db") = selected_db
        ps("sql") = sql
        ps("sql_ctr") = sql_ctr
        ps("sql_time") = (DateTime.Now().Ticks - sql_time) / 10 / 1000 / 1000 '100nano/micro/milliseconds/seconds
        ps("head_fields") = tablehead
        ps("rows") = tablerows
        If tablerows IsNot Nothing Or tablehead IsNot Nothing Then ps("is_results") = True
        Return ps
    End Function

    Public Sub SaveAction()
        fw.routeRedirect("Index")
    End Sub

    Private Function sth2table(sth As DbDataReader) As ArrayList
        If sth Is Nothing OrElse Not sth.HasRows Then Return Nothing
        Dim result As New ArrayList

        While sth.Read()
            Dim tblrow As New Hashtable
            tblrow("fields") = New ArrayList

            For i As Integer = 0 To sth.FieldCount - 1
                Dim tblfld As New Hashtable
                tblfld("value") = sth(i).ToString()

                tblrow("fields").Add(tblfld)
            Next
            result.Add(tblrow)
        End While

        Return result
    End Function

    Private Function sth2head(sth As DbDataReader) As ArrayList
        If sth Is Nothing Then Return Nothing
        Dim result As New ArrayList

        For i As Integer = 0 To sth.FieldCount - 1
            Dim tblfld As New Hashtable
            tblfld("field_name") = sth.GetName(i)

            result.Add(tblfld)
        Next

        Return result
    End Function

    Private Sub show_tables(ByRef tablehead As ArrayList, ByRef tablerows As ArrayList)
        tablehead = New ArrayList
        Dim h As New Hashtable
        h("field_name") = "Table"
        tablehead.Add(h)
        h = New Hashtable
        h("field_name") = "Row Count"
        tablehead.Add(h)

        tablerows = New ArrayList

        Dim conn As DbConnection = db.connect()
        Dim dataTable As DataTable = conn.GetSchema("Tables")
        For Each row As DataRow In dataTable.Rows
            Dim tblname As String = row("TABLE_NAME").ToString()
            If InStr(tblname, "MSys", CompareMethod.Binary) = 0 Then
                Dim tblrow As New Hashtable
                tblrow("fields") = New ArrayList

                Dim tblfld As New Hashtable
                tblfld("db") = db.db_name
                tblfld("value") = tblname
                tblfld("is_select_link") = True
                tblrow("fields").Add(tblfld)

                tblfld = New Hashtable
                tblfld("value") = get_tbl_count(tblname)
                tblrow("fields").Add(tblfld)

                tblrow("db") = db.db_name
                tablerows.Add(tblrow)
            End If
        Next

    End Sub

    Private Function get_tbl_count(ByVal tblname As String) As Integer
        Dim result As Integer = -1
        Try
            result = db.value("select count(*) from [" & tblname & "]")
        Catch ex As Exception

        End Try

        Return result
    End Function

    Private Function strip_comments(ByVal sql As String) As String
        Return Regex.Replace(sql, "/\*.+?\*/", " ", RegexOptions.Singleline)
    End Function

    Private Function split_multi_sql(ByVal sql As String) As String()
        Return Regex.Split(sql, ";[\n\r]+")
    End Function

End Class

