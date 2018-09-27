' Base Admin screens controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2017 Oleg Savchuk www.osalabs.com

Public Class FwAdminController
    Inherits FwController
    'Public Shared Shadows route_default_action As String = "index" 'empty|index|show - calls IndexAction or ShowAction accordingly if no requested controller action found. If empty (default) - show template from /cur_controller/cur_action dir

    'support of customizable view list
    'map of fileld names to screen names
    Public view_list_defaults As String = "" 'qw list of default columns
    Public view_list_map As String = "" 'qh list of all available columns fieldname|visiblename
    Public view_list_map_cache As Hashtable 'cache for view_list_map as hash

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


    '********************* support for customizable list screen
    Public Function UserViewsAction(Optional form_id As String = "") As Hashtable
        Dim ps As New Hashtable

        Dim rows = getViewListArr(getViewListUserFields(), True) 'list all fields
        ''set checked only for those selected by user
        'Dim hfields = Utils.qh(getViewListUserFields())
        'For Each row In rows
        '    row("is_checked") = hfields.ContainsKey(row("field_name"))
        'Next

        ps("rows") = rows
        fw.parser("/common/list/userviews", ps)
    End Function

    Public Sub SaveUserViewsAction()
        Dim item As Hashtable = reqh("item")
        Dim success = True

        Try
            If reqi("is_reset") = 1 Then
                fw.model(Of UserViews).updateByScreen(base_url, view_list_defaults)
            Else
                'save fields
                'order by value
                Dim ordered = reqh("fld").Cast(Of DictionaryEntry).OrderBy(Function(entry) Utils.f2int(entry.Value)).ToList()
                'and then get ordered keys
                Dim anames As New List(Of String)
                For Each el In ordered
                    anames.Add(el.Key)
                Next

                fw.model(Of UserViews).updateByScreen(base_url, Join(anames.ToArray(), " "))
            End If

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        fw.redirect(return_url)
    End Sub

    Public Overridable Function getViewListMap() As Hashtable
        If view_list_map_cache Is Nothing Then view_list_map_cache = Utils.qh(view_list_map)
        Return view_list_map_cache
    End Function

    'as arraylist of hashtables {field_name=>, field_name_visible=> [, is_checked=>true]} in right order
    'if fields defined - show fields only
    'if is_all true - then show all fields (not only from fields param)
    Public Overridable Function getViewListArr(Optional fields As String = "", Optional is_all As Boolean = False) As ArrayList
        Dim result As New ArrayList

        'if fields defined - first show these fields, then the rest
        Dim fields_added As New Hashtable
        If fields > "" Then
            Dim map = getViewListMap()
            For Each fieldname In Utils.qw(fields)
                result.Add(New Hashtable From {{"field_name", fieldname}, {"field_name_visible", map(fieldname)}, {"is_checked", True}})
                fields_added(fieldname) = True
            Next
        End If

        If is_all Then
            'rest/all fields
            Dim arr = Utils.qw(view_list_map)
            For Each v In arr
                v = Replace(v, "&nbsp;", " ")
                Dim asub() As String = Split(v, "|", 2)
                If UBound(asub) < 1 Then Throw New ApplicationException("Wrong Format for view_list_map")
                If fields_added.ContainsKey(asub(0)) Then Continue For

                result.Add(New Hashtable From {{"field_name", asub(0)}, {"field_name_visible", asub(1)}})
            Next
        End If
        Return result
    End Function

    Public Overridable Function getViewListSortmap() As Hashtable
        Dim result As New Hashtable
        For Each fieldname In getViewListMap().Keys
            result(fieldname) = fieldname
        Next
        Return result
    End Function

    Public Overridable Function getViewListUserFields() As String
        Dim item = fw.model(Of UserViews).oneByScreen(base_url) 'base_url is screen identifier
        Return IIf(item("fields") > "", item("fields"), view_list_defaults)
    End Function

    'add to ps:
    ' headers
    ' headers_search
    ' depends on ps("list_rows")
    'usage:
    ' model.setViewList(ps, reqh("search"))
    Public Overridable Sub setViewList(ps As Hashtable, hsearch As Hashtable)
        Dim fields = getViewListUserFields()

        Dim headers = getViewListArr(fields)
        'add search from user's submit
        For Each header As Hashtable In headers
            header("search_value") = hsearch(header("field_name"))
        Next

        ps("headers") = headers
        ps("headers_search") = headers

        'dynamic cols
        For Each row As Hashtable In ps("list_rows")
            Dim cols As New ArrayList
            For Each fieldname In Utils.qw(fields)
                cols.Add(New Hashtable From {
                    {"row", row},
                    {"field_name", fieldname},
                    {"data", row(fieldname)}
                })
            Next
            row("cols") = cols
        Next
    End Sub

End Class
