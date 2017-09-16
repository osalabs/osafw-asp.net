' Categories Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminCategoriesController
    Inherits FwController
    Protected model As New Categories

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        required_fields = "iname"
        base_url = "/Admin/Categories"
    End Sub

    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable

        'get filters
        Dim f As Hashtable = initFilter()

        'sorting
        If f("sortby") = "" Then f("sortby") = "iname"
        If f("sortdir") <> "desc" Then f("sortdir") = "asc"
        Dim SORTSQL As Hashtable = Utils.qh("id|id iname|iname add_time|add_time")

        Dim where As String = " status = 0"
        If f("s") > "" Then
            where &= " and (iname like " & db.q("%" & f("s") & "%") & _
                    " or idesc like " & db.q("%" & f("s") & "%") & ")"
        End If

        hf("count") = db.value("select count(*) from " & model.table_name & " where " & where)
        If hf("count") > 0 Then
            Dim offset As Integer = f("pagenum") * f("pagesize")
            Dim limit As Integer = f("pagesize")
            Dim orderby As String = SORTSQL(f("sortby"))
            If f("sortdir") = "desc" Then orderby = orderby & " desc"

            'offset+1 because _RowNumber starts from 1
            Dim sql As String = "SELECT TOP " & limit & " * " & _
                            " FROM (" & _
                            "   SELECT *, ROW_NUMBER() OVER (ORDER BY " & orderby & ") AS _RowNumber" & _
                            "   FROM " & model.table_name & _
                            "   WHERE " & where & _
                            ") tmp" & _
                        " WHERE _RowNumber >= " & (offset + 1) & _
                        " ORDER BY " & orderby
            'for MySQL this would be much simplier
            '$sql = "SELECT * FROM $table $where ORDER BY $orderby LIMIT " . ($start == 0 ? '' : "$start,") . " $limit";

            hf("list_rows") = db.array(sql)
            hf("pager") = FormUtils.getPager(hf("count"), f("pagenum"), f("pagesize"))
        End If
        hf("f") = f

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
                'item("field")='default value'
            End If
        Else
            'read from db
            item = model.one(id)
            'and merge new values from the form
            Utils.mergeHash(item, reqh("item"))
            'here make additional changes if necessary
        End If

        hf("add_users_id_name") = fw.model(Of Users).getFullName(item("add_users_id"))
        hf("add_users_id_name") = fw.model(Of Users).getFullName(item("add_users_id"))

        hf("id") = id
        hf("i") = item

        Return hf
    End Function

    Public Sub SaveAction(Optional ByVal form_id As String = "")
        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim itemdb As Hashtable = model.one(id)

            item = FormUtils.filter(item, Utils.qw("iname idesc status"))

            If id > 0 Then
                model.update(id, item)
                fw.FLASH("record_updated", 1)
            Else
                id = model.add(item)
                fw.FLASH("record_added", 1)
            End If

            fw.redirect(base_url & "/" & id & "/edit")
        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            Dim args() As [String] = {id}
            fw.route_redirect("ShowForm", Nothing, args)
        End Try
    End Sub

    Public Function Validate(id As String, item As Hashtable) As Boolean
        Dim msg As String = ""
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw(required_fields))
        If Not result Then msg = "Please fill in all required fields"

        If result AndAlso model.isExists(item("iname"), id) Then
            result = False
            fw.FERR("iname") = "EXISTS"
        End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    result = False
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        If Not result Then Throw New ApplicationException(msg)
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
                Model.delete(id)
                ctr += 1
            End If
        Next

        fw.FLASH("multidelete", ctr)
        fw.redirect(base_url)
    End Sub

End Class
