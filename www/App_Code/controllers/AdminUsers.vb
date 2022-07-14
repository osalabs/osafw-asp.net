' Admin Users controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2020 Oleg Savchuk www.osalabs.com

Public Class AdminUsersController
    Inherits FwDynamicController
    Public Shared Shadows access_level As Integer = 90

    Protected model As Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        'use if config doesn't contains model name
        'model0 = fw.model(Of Users)()
        'model = model0

        base_url = "/Admin/Users"
        Me.loadControllerConfig()
        model = model0
        db = model.getDB() 'model-based controller works with model's db

        model_related = fw.model(Of Users)()
    End Sub

    Public Overrides Function ShowFormAction(Optional form_id As String = "") As Hashtable
        Dim ps = MyBase.ShowFormAction(form_id)
        Dim item As Hashtable = ps("i")
        ps("att") = fw.model(Of Att).one(Utils.f2int(item("att_id")))
        Return ps
    End Function

    Public Overrides Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in Controller.save_fields")

        If reqi("refresh") = 1 Then
            fw.routeRedirect("ShowForm", {form_id})
            Return Nothing
        End If

        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)
        Dim success = True
        Dim is_new = (id = 0)

        item("email") = item("ehack") 'just because Chrome autofills fields too agressively

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim item_old As Hashtable = model0.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Me.save_fields)
            If Me.save_fields_checkboxes > "" Then FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes)
            If Me.save_fields_nullable > "" Then FormUtils.filterNullable(itemdb, save_fields_nullable)

            itemdb("pwd") = Trim(itemdb("pwd") & "")
            If Not itemdb("pwd") > "" Then itemdb.Remove("pwd")

            id = Me.modelAddOrUpdate(id, itemdb)

            If model.meId() = id Then model.reloadSession(id)

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.afterSave(success, id, is_new)
    End Function

    Public Overrides Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw(required_fields))
        If Not result Then fw.FERR("REQ") = 1

        If result AndAlso model.isExists(item("email"), id) Then
            result = False
            fw.FERR("ehack") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.isEmail(item("email")) Then
            result = False
            fw.FERR("ehack") = "WRONG"
        End If

        'uncomment if project requires good password strength
        'If result AndAlso item.ContainsKey("pwd") AndAlso model.scorePwd(item("pwd")) <= 60 Then
        '    result = False
        '    fw.FERR("pwd") = "BAD"
        'End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    result = False
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        If fw.FERR.Count > 0 AndAlso Not fw.FERR.ContainsKey("REQ") Then
            fw.FERR("INVALID") = 1
        End If

        If Not result Then Throw New ApplicationException("")
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

        ps("success") = model.sendPwdReset(id)
        ps("err_msg") = fw.last_error_send_email
        ps("_json") = True
        Return ps
    End Function

    'for migration to hashed passwords
    Public Sub HashPasswordsAction()
        rw("hashing passwords")
        Dim rows = db.array("select id, pwd from " & db.q_ident(model.table_name) & " order by id")
        For Each row As Hashtable In rows
            If Left(row("pwd"), 2) = "$2" Then Continue For 'already hashed
            Dim hashed = model.hashPwd(row("pwd"))
            db.update(model.table_name, New Hashtable From {{"pwd", hashed}}, New Hashtable From {{"id", row("id")}})
        Next
        rw("done")
    End Sub

End Class
