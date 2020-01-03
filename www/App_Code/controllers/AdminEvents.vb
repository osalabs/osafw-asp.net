' Events Log Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminEventsController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Protected model As New FwEvents
    Protected model_users As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        model_users.init(fw)
        required_fields = "iname" 'default required fields, space-separated
        base_url = "/Admin/Events" 'base url for the controller
    End Sub

    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable
        db.connect()

        'get filters
        Dim f As Hashtable = initFilter()

        If Not reqs("dofilter") > "" AndAlso f("date") = "" Then
            f("date") = DateUtils.Date2Str(Now())
        End If

        'sorting
        If f("sortby") = "" Then f("sortby") = "id"
        If f("sortdir") <> "desc" Then f("sortdir") = "desc"
        Dim SORTSQL As Hashtable = Utils.qh("id|id iname|iname add_time|add_time")

        Dim where As String = " 1=1 "
        If f("s") > "" Then
            where &= " and (iname like " & db.q("%" & f("s") & "%") & _
                    " or item_id=" & db.qi(f("s")) & _
                    ")"
        End If
        If f("events_id") > "" Then
            where &= " and events_id = " & db.qi(f("events_id"))
        End If
        If f("users_id") > "" Then
            where &= " and add_users_id = " & db.qi(f("users_id"))
        End If
        If f("date") > "" Then
            Dim d As String = db.qone(model.log_table_name, "add_time", f("date"))
            where &= " and add_time >= " & d & " and add_time < DATEADD(DAY, 1, " & d & ")"
        End If

        hf("count") = db.value("select count(*) from " & model.log_table_name & " where " & where)
        If hf("count") > 0 Then
            Dim offset As Integer = f("pagenum") * f("pagesize")
            Dim limit As Integer = f("pagesize")
            Dim orderby As String = SORTSQL(f("sortby"))
            If Not orderby > "" Then Throw New Exception("No orderby defined for [" & f("sortby") & "]")
            If f("sortdir") = "desc" Then
                If InStr(orderby, ",") Then orderby = Replace(orderby, ",", " desc,")
                orderby &= " desc"
            End If

            'offset+1 because _RowNumber starts from 1
            Dim sql As String = "SELECT TOP " & limit & " * " & _
                            " FROM (" & _
                            "   SELECT *, ROW_NUMBER() OVER (ORDER BY " & orderby & ") AS _RowNumber" & _
                            "   FROM " & model.log_table_name & _
                            "   WHERE " & where & _
                            ") tmp" & _
                        " WHERE _RowNumber >= " & (offset + 1) & _
                        " ORDER BY " & orderby

            hf("list_rows") = db.array(sql)
            hf("pager") = FormUtils.getPager(hf("count"), f("pagenum"), f("pagesize"))

            For Each row As Hashtable In hf("list_rows")
                row("user") = model_users.one(row("add_users_id"))
                row("event") = model.one(row("events_id"))
            Next
        End If
        hf("f") = f
        hf("filter_select_events") = model.listSelectOptions()
        hf("filter_select_users") = model_users.listSelectOptions()

        Return hf
    End Function



End Class
