' Fw Dynamic controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class FwDynamicController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Protected model_related As FwModel

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'uncomment in the interited controller
        'base_url = "/Admin/DemosDynamic" 'base url must be defined for loadControllerConfig
        'Me.loadControllerConfig()
        'model_related = fw.model(Of FwModel)()

        'hack to sort properly by formatted dates
        'so in list view you have: FORMAT(DATE_FIELD, 'MM/dd/yyyy') as DATE_FIELD_str
        'list_sortmap("DATE_FIELD_str") = "DATE_FIELD"
    End Sub

    Public Overridable Function IndexAction() As Hashtable
        'get filters from the search form
        Dim f As Hashtable = Me.initFilter()

        Me.setListSorting()

        Me.setListSearch()
        Me.setListSearchStatus() 'status field is not always in table, so keep it separate
        'set here non-standard search
        'If f("field") > "" Then
        '    Me.list_where &= " and field=" & db.q(f("field"))
        'End If

        Me.getListRows()
        'add/modify rows from db if necessary
        'For Each row As Hashtable In Me.list_rows
        '    row("field") = "value"
        'Next

        'set standard output parse strings
        Dim ps = Me.setPS()

        'userlists support if necessary
        If Me.is_userlists Then Me.setUserLists(ps)

        ps("select_userfilters") = fw.model(Of UserFilters).listSelectByIcode(fw.G("controller.action"))

        If is_dynamic_index Then
            'customizable headers
            setViewList(ps, reqh("search"))
        End If

        If reqs("export") > "" Then
            exportList()
        Else
            Return ps
        End If
    End Function

    Public Overridable Function ShowAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)
        Dim item As Hashtable = model0.one(id)
        If item.Count = 0 Then Throw New ApplicationException("Not Found")

        'added/updated should be filled before dynamic fields
        setAddUpdUser(ps, item)

        'dynamic fields
        If is_dynamic_show Then ps("fields") = prepareShowFields(item, ps)

        'userlists support if necessary
        If Me.is_userlists Then Me.setUserLists(ps, id)

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id
        ps("base_url") = base_url
        ps("is_userlists") = is_userlists

        Return ps
    End Function

    Public Overridable Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'define form_new_defaults via config.json
        'Me.form_new_defaults = New Hashtable From {{"field", "default value"}} 'OR set new form defaults here

        Dim ps As New Hashtable
        Dim item = reqh("item") 'set defaults from request params
        Dim id = Utils.f2int(form_id)

        If isGet() Then 'read from db
            If id > 0 Then
                item = model0.one(id)
                'item("ftime_str") = FormUtils.int2timestr(item("ftime")) 'TODO - refactor this
            Else
                'override any defaults here
                If Me.form_new_defaults IsNot Nothing Then
                    Utils.mergeHash(item, Me.form_new_defaults)
                End If
            End If
        Else
            'read from db
            Dim itemdb = model0.one(id)
            'and merge new values from the form
            Utils.mergeHash(itemdb, item)
            item = itemdb
            'here make additional changes if necessary
        End If

        setAddUpdUser(ps, item)

        If is_dynamic_showform Then ps("fields") = prepareShowFormFields(item, ps)
        'TODO
        'ps("select_options_parent_id") = model.listSelectOptionsParent()
        'FormUtils.comboForDate(item("fdate_combo"), ps, "fdate_combo")

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id
        If fw.FERR.Count > 0 Then logger(fw.FERR)

        Return ps
    End Function

    Public Overrides Function modelAddOrUpdate(id As Integer, fields As Hashtable) As Integer
        If is_dynamic_showform Then processSaveShowFormFields(id, fields)

        id = MyBase.modelAddOrUpdate(id, fields)

        If is_dynamic_showform Then processSaveShowFormFieldsAfter(id, fields)

        Return id
    End Function

    Public Overridable Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in Controller.save_fields")

        If reqi("refresh") = 1 Then
            fw.routeRedirect("ShowForm", {form_id})
            Return Nothing
        End If

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
            If Me.save_fields_nullable > "" Then FormUtils.filterNullable(itemdb, save_fields_nullable)

            id = Me.modelAddOrUpdate(id, itemdb)
        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.afterSave(success, id, is_new)
    End Function

    ''' <summary>
    ''' Performs submitted form validation for required field and simple validations: exits, isemail, isphone, isdate, isfloat.
    ''' If more complex validation required - just override this and call just necessary validation
    ''' </summary>
    ''' <param name="id"></param>
    ''' <param name="item"></param>
    Public Overridable Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = validateRequiredDynamic(item)

        If result AndAlso is_dynamic_showform Then validateSimpleDynamic(id, item)

        'If result AndAlso Not SomeOtherValidation() Then
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        Me.validateCheckResult()
    End Sub

    Protected Overridable Function validateRequiredDynamic(item As Hashtable) As Boolean
        Dim result = True
        If String.IsNullOrEmpty(Me.required_fields) AndAlso is_dynamic_showform Then
            'if required_fields not defined - fill from showform_fields
            Dim fields As ArrayList = Me.config("showform_fields")
            Dim req As New ArrayList
            For Each def As Hashtable In fields
                If Utils.f2bool(def("required")) Then req.Add(def("field"))
            Next

            If req.Count > 0 Then result = Me.validateRequired(item, req.ToArray())
        Else
            result = Me.validateRequired(item, Me.required_fields)
        End If
        Return result
    End Function

    'simple validation via showform_fields
    Protected Overridable Function validateSimpleDynamic(id As Integer, item As Hashtable) As Boolean
        Dim result As Boolean = True
        Dim fields As ArrayList = Me.config("showform_fields")
        For Each def As Hashtable In fields
            Dim field = def("field")
            If field = "" Then Continue For

            Dim val = Utils.qh(def("validate"))
            If val.ContainsKey("exists") AndAlso model0.isExistsByField(item(field), id, field) Then
                fw.FERR(field) = "EXISTS"
                result = False
            End If
            If val.ContainsKey("isemail") AndAlso Not FormUtils.isEmail(item(field)) Then
                fw.FERR(field) = "WRONG"
                result = False
            End If
            If val.ContainsKey("isphone") AndAlso Not FormUtils.isPhone(item(field)) Then
                fw.FERR(field) = "WRONG"
                result = False
            End If
            If val.ContainsKey("isdate") AndAlso Not Utils.isDate(item(field)) Then
                fw.FERR(field) = "WRONG"
                result = False
            End If
            If val.ContainsKey("isfloat") AndAlso Not Utils.isFloat(item(field)) Then
                fw.FERR(field) = "WRONG"
                result = False
            End If

            'If Not result Then Exit For 'uncomment to break on first error
        Next
        Return result
    End Function


    Public Overridable Sub ShowDeleteAction(ByVal form_id As String)
        Dim id As Integer = Utils.f2int(form_id)

        Dim ps = New Hashtable From {
            {"i", model0.one(id)},
            {"related_id", Me.related_id},
            {"return_url", Me.return_url},
            {"base_url", Me.base_url}
        }

        fw.parser("/common/form/showdelete", ps)
    End Sub

    Public Overridable Function DeleteAction(ByVal form_id As String) As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        Dim item = model0.one(id)
        'if record already deleted and we are admin - perform permanent delete
        If Not String.IsNullOrEmpty(model0.field_status) AndAlso Utils.f2int(item(model0.field_status)) = FwModel.STATUS_DELETED AndAlso fw.model(Of Users).checkAccess(Users.ACL_ADMIN, False) Then
            model0.delete(id, True)
        Else
            model0.delete(id)
        End If

        fw.FLASH("onedelete", 1)
        Return Me.afterSave(True)
    End Function

    Public Overridable Function RestoreDeletedAction(ByVal form_id As String) As Hashtable
        Dim id As Integer = Utils.f2int(form_id)

        model0.update(id, New Hashtable From {{model0.field_status, FwModel.STATUS_ACTIVE}})

        fw.FLASH("record_updated", 1)
        Return Me.afterSave(True, id)
    End Function

    Public Overridable Function SaveMultiAction() As Hashtable
        Dim cbses As Hashtable = reqh("cb")
        Dim is_delete As Boolean = fw.FORM.ContainsKey("delete")
        Dim user_lists_id As Integer = reqi("addtolist")
        Dim remove_user_lists_id = reqi("removefromlist")
        Dim ctr As Integer = 0

        If user_lists_id > 0 Then
            Dim user_lists = fw.model(Of UserLists).one(user_lists_id)
            If user_lists.Count = 0 OrElse user_lists("add_users_id") <> fw.model(Of Users).meId Then Throw New ApplicationException("Wrong Request")
        End If

        For Each id As String In cbses.Keys
            If is_delete Then
                model0.delete(id)
                ctr += 1
            ElseIf user_lists_id > 0 Then
                fw.model(Of UserLists).addItemList(user_lists_id, id)
                ctr += 1
            ElseIf remove_user_lists_id > 0 Then
                fw.model(Of UserLists).delItemList(remove_user_lists_id, id)
                ctr += 1
            End If
        Next

        If is_delete Then fw.FLASH("multidelete", ctr)
        If user_lists_id > 0 Then fw.FLASH("success", ctr & " records added to the list")

        Return Me.afterSave(True, New Hashtable From {{"ctr", ctr}})
    End Function


    '********************* support for autocomlete related items
    Public Overridable Function AutocompleteAction() As Hashtable
        If model_related Is Nothing Then Throw New ApplicationException("No model_related defined")
        Dim items As ArrayList = model_related.getAutocompleteList(reqs("q"))

        Return New Hashtable From {{"_json", items}}
    End Function

    '********************* support for customizable list screen
    Public Overridable Sub UserViewsAction(Optional form_id As String = "")
        Dim ps As New Hashtable

        Dim rows = getViewListArr(getViewListUserFields(), True) 'list all fields
        ''set checked only for those selected by user
        'Dim hfields = Utils.qh(getViewListUserFields())
        'For Each row In rows
        '    row("is_checked") = hfields.ContainsKey(row("field_name"))
        'Next

        ps("rows") = rows
        fw.parser("/common/list/userviews", ps)
    End Sub

    Public Overridable Sub SaveUserViewsAction()
        Dim fld As Hashtable = reqh("fld")
        Dim success = True

        Try
            If reqi("is_reset") = 1 Then
                fw.model(Of UserViews).updateByIcode(base_url, view_list_defaults)
            Else
                'save fields
                'order by value
                Dim ordered = fld.Cast(Of DictionaryEntry).OrderBy(Function(entry) Utils.f2int(entry.Value)).ToList()
                'and then get ordered keys
                Dim anames As New List(Of String)
                For Each el In ordered
                    anames.Add(el.Key)
                Next

                fw.model(Of UserViews).updateByIcode(base_url, Join(anames.ToArray(), " "))
            End If

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        fw.redirect(return_url)
    End Sub

    '''''' HELPERS for dynamic fields

    ''' <summary>
    ''' prepare data for fields repeat in ShowAction based on config.json show_fields parameter
    ''' </summary>
    ''' <param name="item"></param>
    ''' <param name="ps"></param>
    ''' <returns></returns>
    Public Overridable Function prepareShowFields(item As Hashtable, ps As Hashtable) As ArrayList
        Dim id = Utils.f2int(item("id"))

        Dim fields As ArrayList = Me.config("show_fields")
        For Each def As Hashtable In fields
            def("i") = item 'ref to item
            Dim dtype = def("type")
            Dim field = def("field")

            If dtype = "row" OrElse dtype = "row_end" OrElse dtype = "col" OrElse dtype = "col_end" Then
                'structural tags
                def("is_structure") = True

            ElseIf dtype = "multi" Then
                'complex field
                If def("table_link") > "" Then
                    'def("multi_datarow") = fw.model(def("lookup_model")).getMultiListAL(model0.getLinkedIds(def("table_link"), id, def("table_link_id_name"), def("table_link_linked_id_name")), def)
                    def("multi_datarow") = fw.model(def("lookup_model")).getMultiListAL(model0.getLinkedIdsByDef(id, def), def)
                Else
                    def("multi_datarow") = fw.model(def("lookup_model")).getMultiList(item(field), def)
                End If

            ElseIf dtype = "multi_prio" Then
                'complex field with prio
                def("multi_datarow") = fw.model(def("lookup_model")).getMultiListLinkedRows(id, def)

            ElseIf dtype = "att" Then
                def("att") = fw.model(Of Att).one(Utils.f2int(item(field)))

            ElseIf dtype = "att_links" Then
                def("att_links") = fw.model(Of Att).getAllLinked(model0.table_name, Utils.f2int(id))

            Else
                'single values
                'lookups
                If def.ContainsKey("lookup_table") Then 'lookup by table
                    Dim lookup_key = def("lookup_key")
                    If lookup_key = "" Then lookup_key = "id"

                    Dim lookup_field = def("lookup_field")
                    If lookup_field = "" Then lookup_field = "iname"

                    def("lookup_row") = db.row(def("lookup_table"), New Hashtable From {{lookup_key, item(field)}})
                    def("value") = def("lookup_row")(lookup_field)

                ElseIf def.ContainsKey("lookup_model") Then 'lookup by model
                    Dim lookup_model = fw.model(def("lookup_model"))
                    def("lookup_id") = Utils.f2int(item(field))
                    def("lookup_row") = lookup_model.one(def("lookup_id"))

                    Dim lookup_field = def("lookup_field")
                    If lookup_field = "" Then lookup_field = lookup_model.field_iname

                    def("value") = def("lookup_row")(lookup_field)
                    If Not def.ContainsKey("admin_url") Then def("admin_url") = "/Admin/" & def("lookup_model") 'default admin url from model name

                ElseIf def.ContainsKey("lookup_tpl") Then
                    def("value") = FormUtils.selectTplName(def("lookup_tpl"), item(field))

                Else
                    def("value") = item(field)
                End If

                'convertors
                If def.ContainsKey("conv") Then
                    If def("conv") = "time_from_seconds" Then
                        def("value") = FormUtils.intToTimeStr(Utils.f2int(def("value")))
                    End If
                End If
            End If
        Next
        Return fields
    End Function

    Public Overridable Function prepareShowFormFields(item As Hashtable, ps As Hashtable) As ArrayList
        Dim id = Utils.f2int(item("id"))

        Dim fields As ArrayList = Me.config("showform_fields")
        If fields Is Nothing Then Throw New ApplicationException("Controller config.json doesn't contain 'showform_fields'")
        For Each def As Hashtable In fields
            'logger(def)
            def("i") = item 'ref to item
            def("ps") = ps 'ref to whole ps
            Dim dtype = def("type") 'type is required
            Dim field = def("field") & ""

            If id = 0 AndAlso (dtype = "added" OrElse dtype = "updated") Then
                'special case - hide if new item screen
                def("class") = "d-none"
            End If

            If dtype = "row" OrElse dtype = "row_end" OrElse dtype = "col" OrElse dtype = "col_end" Then
                'structural tags
                def("is_structure") = True

            ElseIf dtype = "multicb" Then
                'complex field
                If def("table_link") > "" Then
                    '  model0.getLinkedIds(def("table_link"), id, def("table_link_id_name"), def("table_link_linked_id_name"))
                    def("multi_datarow") = fw.model(def("lookup_model")).getMultiListAL(model0.getLinkedIdsByDef(id, def), def)
                Else
                    def("multi_datarow") = fw.model(def("lookup_model")).getMultiList(item(field), def)
                End If

                For Each row As Hashtable In def("multi_datarow") 'contains id, iname, is_checked
                    row("field") = def("field")
                Next
            ElseIf dtype = "multicb_prio" Then
                def("multi_datarow") = fw.model(def("lookup_model")).getMultiListLinkedRows(id, def)

                For Each row As Hashtable In def("multi_datarow") 'contains id, iname, is_checked, _link[prio]
                    row("field") = def("field")
                Next

            ElseIf dtype = "att_edit" Then
                def("att") = fw.model(Of Att).one(Utils.f2int(item(field)))
                def("value") = item(field)

            ElseIf dtype = "att_links_edit" Then
                def("att_links") = fw.model(Of Att).getAllLinked(model0.table_name, Utils.f2int(id))

            Else
                'single values
                'lookups
                If def.ContainsKey("lookup_table") Then 'lookup by table
                    Dim lookup_key = def("lookup_key")
                    If lookup_key = "" Then lookup_key = "id"

                    Dim lookup_field = def("lookup_field")
                    If lookup_field = "" Then lookup_field = "iname"

                    def("lookup_row") = db.row(def("lookup_table"), New Hashtable From {{lookup_key, item(field)}})
                    def("value") = def("lookup_row")(lookup_field)

                ElseIf def.ContainsKey("lookup_model") Then 'lookup by model
                    If def.ContainsKey("lookup_field") Then
                        'lookup value
                        def("lookup_row") = fw.model(def("lookup_model")).one(Utils.f2int(item(field)))
                        def("value") = def("lookup_row")(def("lookup_field"))
                    Else
                        'lookup select
                        def("select_options") = fw.model(def("lookup_model")).listSelectOptions(def)
                        def("value") = item(field)
                    End If

                ElseIf def.ContainsKey("lookup_tpl") Then
                    def("select_options") = FormUtils.selectTplOptions(def("lookup_tpl"))
                    def("value") = item(field)
                    For Each row As Hashtable In def("select_options") 'contains id, iname
                        row("is_inline") = def("is_inline")
                        row("field") = def("field")
                        row("value") = item(field)
                    Next

                Else
                    def("value") = item(field)
                End If

                'convertors
                If def.ContainsKey("conv") Then
                    If def("conv") = "time_from_seconds" Then
                        def("value") = FormUtils.intToTimeStr(Utils.f2int(def("value")))
                    End If
                End If
            End If
        Next
        Return fields
    End Function

    'auto-process fields BEFORE record saved to db
    Protected Overridable Sub processSaveShowFormFields(id As Integer, fields As Hashtable)
        Dim item As Hashtable = reqh("item")

        Dim showform_fields = _fieldsToHash(Me.config("showform_fields"))

        Dim fnullable = Utils.qh(save_fields_nullable)

        'special auto-processing for fields of particular types
        For Each field As String In fields.Keys.Cast(Of String).ToArray()
            If showform_fields.ContainsKey(field) Then
                Dim def = showform_fields(field)
                If def("type") = "multicb" Then
                    'multiple checkboxes
                    fields(field) = FormUtils.multi2ids(reqh(field & "_multi"))
                ElseIf def("type") = "autocomplete" Then
                    fields(field) = fw.model(def("lookup_model")).findOrAddByIname(fields(field))
                ElseIf def("type") = "date_combo" Then
                    fields(field) = FormUtils.dateForCombo(item, field)
                ElseIf def("type") = "time" Then
                    fields(field) = FormUtils.timeStrToInt(fields(field)) 'ftime - convert from HH:MM to int (0-24h in seconds)
                ElseIf def("type") = "number" Then
                    If fnullable.ContainsKey(field) AndAlso fields(field) = "" Then
                        'if field nullable and empty - pass NULL
                        fields(field) = Nothing
                    Else
                        fields(field) = Utils.f2float(fields(field)) 'number - convert to number (if field empty or non-number - it will become 0)
                    End If
                End If
            End If
        Next

        'fields("fint") = Utils.f2int(fields("fint")) 'TODO? field accepts only int

    End Sub

    'auto-process fields AFTER record saved to db
    Protected Overridable Sub processSaveShowFormFieldsAfter(id As Integer, fields As Hashtable)

        'for now we just look if we have att_links_edit field and update att links
        For Each def As Hashtable In Me.config("showform_fields")
            If def("type") = "att_links_edit" Then
                fw.model(Of Att).updateAttLinks(model0.table_name, id, reqh("att")) 'TODO make att configurable
            ElseIf def("type") = "multicb" Then
                If def("table_link") > "" Then
                    model0.updateLinked(def("table_link"), id, def("table_link_id_name"), def("table_link_linked_id_name"), reqh(def("field") & "_multi"))
                End If
            ElseIf def("type") = "multicb_prio" Then
                fw.model(def("lookup_model")).updateLinkedRows(id, reqh(def("field") & "_multi"))
            End If
        Next

    End Sub

    'convert config's fields list into hashtable as field => {}
    'if there are more than one field - just first field added to the hash
    Protected Function _fieldsToHash(fields As ArrayList) As Hashtable
        Dim result As New Hashtable
        For Each fldinfo As Hashtable In fields
            If fldinfo.ContainsKey("field") AndAlso Not result.ContainsKey(fldinfo("field")) Then
                result(fldinfo("field")) = fldinfo
            End If
        Next
        Return result
    End Function

End Class
