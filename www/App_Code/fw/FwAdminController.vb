' Base Admin screens controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class FwAdminController
    Inherits FwController

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'DEFINE in inherited controllers like this:
        'base_url = "/Admin/Base"
        'base_url_suffix = "?parent_id=123"
        'required_fields = "iname"
        'save_fields = "iname idesc status"

        'search_fields = "iname idesc"
        'list_sortdef = "iname asc"
        'list_sortmap = Utils.qh("id|id iname|iname add_time|add_time")
    End Sub

    Public Overridable Function IndexAction() As Hashtable
        'get filters from the search form
        Dim f As Hashtable = Me.get_filter()

        Me.set_list_sorting()

        Me.set_list_search()
        'set here non-standard search
        'If f("field") > "" Then
        '    Me.list_where &= " and field=" & db.q(f("field"))
        'End If

        Me.get_list_rows()
        'add/modify rows from db if necessary
        'For Each row As Hashtable In Me.list_rows
        '    row("field") = "value"
        'Next

        Dim ps As Hashtable = New Hashtable
        ps("list_rows") = Me.list_rows
        ps("count") = Me.list_count
        ps("pager") = Me.list_pager
        ps("f") = Me.list_filter

        Return ps
    End Function

    ''' <summary>
    ''' Shows editable Form for adding or editing one entity row
    ''' </summary>
    ''' <param name="form_id"></param>
    ''' <returns>in Hashtable:
    ''' id - id of the entity
    ''' i - hashtable of entity fields
    ''' </returns>
    ''' <remarks></remarks>
    Public Overridable Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As Hashtable = New Hashtable
        Dim item As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        If fw.cur_method = "GET" Then 'read from db
            If id > 0 Then
                item = model0.one(id)
                'item("ftime_str") = FormUtils.int2timestr(item("ftime")) 'TODO - refactor this
            Else
                'set defaults here
                item = New Hashtable
                If Me.form_new_defaults IsNot Nothing Then
                    Utils.hash_merge(item, Me.form_new_defaults)
                End If
            End If
        Else
            'read from db
            item = model0.one(id)
            'and merge new values from the form
            Utils.hash_merge(item, fw.FORM("item"))
            'here make additional changes if necessary
        End If

        ps("add_user_id_name") = fw.model(Of Users).full_name(item("add_user_id"))
        ps("upd_user_id_name") = fw.model(Of Users).full_name(item("upd_user_id"))

        ps("id") = id
        ps("i") = item

        Return ps
    End Function

    Public Overridable Sub SaveAction(Optional ByVal form_id As String = "")
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

        Dim item As Hashtable = req("item")
        Dim id As Integer = Utils.f2int(form_id)

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim item_old As Hashtable = model0.one(id)

            Dim itemdb As Hashtable = FormUtils.form2dbhash(item, Me.save_fields)
            If Me.save_fields_checkboxes > "" Then FormUtils.form2dbhash_checkboxes(itemdb, item, save_fields_checkboxes)

            id = Me.model_add_or_update(id, itemdb)

            fw.redirect(base_url & "/" & id & "/edit" & base_url_suffix)
        Catch ex As ApplicationException
            Me.set_form_error(ex)
            fw.route_redirect("ShowForm", New String() {id})
        End Try
    End Sub

    Public Overridable Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validate_required(item, Me.required_fields)

        'If result AndAlso model0.is_exists(item("iname"), id) Then
        '    fw.FERR("iname") = "EXISTS"
        'End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        Me.validate_check_result()
    End Sub

    Public Overridable Function ShowDeleteAction(ByVal form_id As String) As Hashtable
        Dim ps As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        ps("i") = model0.one(id)
        Return ps
    End Function

    Public Overridable Sub DeleteAction(ByVal form_id As String)
        Dim id As Integer = Utils.f2int(form_id)

        model0.delete(id)
        fw.FLASH("onedelete", 1)
        fw.redirect(base_url & base_url_suffix)
    End Sub

    Public Overridable Sub SaveMultiAction()
        Dim cbses As Hashtable = fw.FORM("cb")
        If cbses Is Nothing Then cbses = New Hashtable
        Dim is_delete As Boolean = fw.FORM.ContainsKey("delete")
        Dim ctr As Integer = 0

        For Each id As String In cbses.Keys
            If is_delete Then
                model0.delete(id)
                ctr += 1
            End If
        Next

        fw.FLASH("multidelete", ctr)
        fw.redirect(base_url & base_url_suffix)
    End Sub

End Class
