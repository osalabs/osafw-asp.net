' Admin Users controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class AdminUsersController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Protected model As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)

        required_fields = "email access_level"
        base_url = "/Admin/Users"
    End Sub

    Public Function IndexAction() As Hashtable
        Dim hf As Hashtable = New Hashtable

        If fw.cur_format = "csv" Then
            ExportAction()
            Return Nothing
        End If

        'get filters
        Dim f As Hashtable = initFilter()

        'sorting
        If f("sortby") = "" Then f("sortby") = "iname"
        If f("sortdir") <> "desc" Then f("sortdir") = "asc"
        Dim SORTSQL As Hashtable = Utils.qh("id|id iname|fname,lname email|email access_level|access_level add_time|add_time status|status")

        Dim where As String = ""
        If list_filter("status") > "" Then
            where &= " status=" & db.qi(list_filter("status"))
        Else
            where &= " status<>127 "
        End If

        If f("s") > "" Then
            where &= " and (email like " & db.q("%" & f("s") & "%") &
                    " or fname like " & db.q("%" & f("s") & "%") &
                    " or lname like " & db.q("%" & f("s") & "%") &
                    ")"
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
        hf("upd_users_id_name") = fw.model(Of Users).getFullName(item("upd_users_id"))

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
            'Dim itemold As Hashtable = model.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Utils.qw("email fname lname phone pwd access_level title address1 address2 city state zip phone status"))

            If id > 0 Then
                If Not itemdb("pwd") > "" Then itemdb.Remove("pwd")
                model.update(id, itemdb)
                fw.FLASH("record_updated", 1)
            Else
                id = model.add(itemdb)
                fw.FLASH("record_added", 1)
            End If

            If id = model.meId() Then model.reloadSession()

            fw.redirect(base_url & "/" & id & "/edit")
        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            Dim args() As [String] = {id}
            fw.route_redirect("ShowForm", Nothing, args)
        End Try
    End Sub

    Public Function Validate(id As String, item As Hashtable) As Boolean
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw(required_fields))
        If Not result Then fw.FERR("REQ") = 1

        If result AndAlso model.isExists(item("email"), id) Then
            result = False
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.isEmail(item("email")) Then
            result = False
            fw.FERR("email") = "WRONG"
        End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    result = False
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

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
                Model.delete(id)
                ctr += 1
            End If
        Next

        fw.FLASH("multidelete", ctr)
        fw.redirect(base_url)
    End Sub

    Public Shadows Sub ExportAction()
        fw.resp.AppendHeader("Content-type", "text/csv")
        fw.resp.AppendHeader("Content-Disposition", "attachment; filename=""" & model.table_name & ".csv""")

        fw.resp.Write(model.getCSVExport())
    End Sub

    'cleanup session for current user and re-login as user from id
    'check access - only users with higher level may login as lower leve
    Public Sub SimulateAction(form_id As String)
        Dim id As Integer = Utils.f2int(form_id)

        Dim user As Hashtable = model.one(id)
        If user.Count = 0 Then Throw New ApplicationException("Wrong User ID")
        If user("access_level") >= fw.SESSION("access_level") Then Throw New ApplicationException("Access Denied. Cannot simulate user with higher access level")

        fw.logEvent("simulate", id, model.meId)

        If model.doLogin(id) Then
            fw.redirect(fw.config("LOGGED_DEFAULT_URL"))
        End If

    End Sub

    Public Function SendPwdAction(form_id As String) As Hashtable
        Dim ps As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        Dim hU As Hashtable = model.one(id)

        fw.send_email_tpl(hU("email"), "email_pwd.txt", hU)

        ps("success") = True
        ps("_json") = True
        Return ps
    End Function
End Class
