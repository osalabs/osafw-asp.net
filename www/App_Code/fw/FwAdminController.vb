' Base Admin screens controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2017 Oleg Savchuk www.osalabs.com

Public Class FwAdminController
    Inherits FwController
    'Public Shared Shadows route_default_action As String = "index" 'empty|index|show - calls IndexAction or ShowAction accordingly if no requested controller action found. If empty (default) - show template from /cur_controller/cur_action dir

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'DEFINE in inherited controllers like this:
        'base_url = "/Admin/Base"
        'required_fields = "iname"
        'save_fields = "iname idesc status"
        'save_fields_checkboxes = ""

        'search_fields = "iname idesc"
        'list_sortdef = "iname asc"
        'list_sortmap = Utils.qh("id|id iname|iname add_time|add_time")

        'list_view = model0.table_name 'optionally override list view/table
    End Sub

    Public Overridable Function IndexAction() As Hashtable
        'get filters from the search form
        Dim f As Hashtable = Me.initFilter()

        Me.setListSorting()

        Me.setListSearch()
        'set here non-standard search
        'If f("field") > "" Then
        '    Me.list_where &= " and field=" & db.q(f("field"))
        'End If

        Me.getListRows()
        'add/modify rows from db if necessary
        'For Each row As Hashtable In Me.list_rows
        '    row("field") = "value"
        'Next

        Dim ps As Hashtable = New Hashtable From {
            {"list_rows", Me.list_rows},
            {"count", Me.list_count},
            {"pager", Me.list_pager},
            {"f", Me.list_filter},
            {"related_id", Me.related_id},
            {"return_url", Me.return_url}
        }

        Return ps
    End Function

    Public Overridable Function ShowAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)
        Dim item As Hashtable = model0.one(id)
        If item.Count = 0 Then Throw New ApplicationException("Not Found")

        ps("add_users_id_name") = fw.model(Of Users).getFullName(item("add_users_id"))
        ps("upd_users_id_name") = fw.model(Of Users).getFullName(item("upd_users_id"))

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id

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
        Dim ps As New Hashtable
        Dim item As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        If fw.cur_method = "GET" Then 'read from db
            If id > 0 Then
                item = model0.one(id)
                'item("ftime_str") = FormUtils.int2timestr(item("ftime")) 'TODO - refactor this
            Else
                'set defaults here
                item = New Hashtable
                'item = reqh("item") 'optionally set defaults from request params
                If Me.form_new_defaults IsNot Nothing Then
                    Utils.mergeHash(item, Me.form_new_defaults)
                End If
            End If
        Else
            'read from db
            item = model0.one(id)
            'and merge new values from the form
            Utils.mergeHash(item, reqh("item"))
            'here make additional changes if necessary
        End If

        ps("add_users_id_name") = fw.model(Of Users).getFullName(item("add_users_id"))
        ps("upd_users_id_name") = fw.model(Of Users).getFullName(item("upd_users_id"))

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id

        Return ps
    End Function

    Public Overridable Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in Controller.save_fields")

        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)
        Dim success = True
        Dim is_new = (id = 0)

        Try
            Validate(id, item)
            'load old record if necessary
            'Dim item_old As Hashtable = model0.one(id)

            Dim itemdb As Hashtable = FormUtils.filter(item, Me.save_fields)
            If Me.save_fields_checkboxes > "" Then FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes)

            id = Me.modelAddOrUpdate(id, itemdb)
        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.saveCheckResult(success, id, is_new)
    End Function

    Public Overridable Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validateRequired(item, Me.required_fields)

        'If result AndAlso model0.isExists(item("iname"), id) Then
        '    fw.FERR("iname") = "EXISTS"
        'End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        Me.validateCheckResult()
    End Sub

    Public Overridable Sub ShowDeleteAction(ByVal form_id As String)
        Dim id As Integer = Utils.f2int(form_id)

        Dim ps = New Hashtable From {
            {"i", model0.one(id)},
            {"related_id", Me.related_id},
            {"return_url", Me.return_url}
        }

        fw.parser("/common/form/showdelete", ps)
    End Sub

    Public Overridable Function DeleteAction(ByVal form_id As String) As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        model0.delete(id)
        fw.FLASH("onedelete", 1)
        Return Me.saveCheckResult(True, id)
    End Function

    Public Overridable Function SaveMultiAction() As Hashtable
        Dim cbses As Hashtable = reqh("cb")
        Dim is_delete As Boolean = fw.FORM.ContainsKey("delete")
        Dim ctr As Integer = 0

        For Each id As String In cbses.Keys
            If is_delete Then
                model0.delete(id)
                ctr += 1
            End If
        Next

        fw.FLASH("multidelete", ctr)
        Return Me.saveCheckResult(True, New Hashtable From {{"ctr", ctr}})
    End Function

End Class
