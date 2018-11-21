' Fw Dynamic controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class FwDynamicController
    Inherits FwController
    Protected model_related As FwModel

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'uncomment in the interited controller
        'base_url = "/Admin/DemosDynamic" 'base url must be defined for loadControllerConfig
        'Me.loadControllerConfig()
        'model_related = fw.model(Of FwModel)()
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

        'userlists support
        ps("select_userlists") = fw.model(Of UserLists).listSelectByEntity(list_view)
        ps("mylists") = fw.model(Of UserLists).listForItem(list_view, 0)
        ps("list_view") = list_view

        If is_dynamic Then
            'customizable headers
            setViewList(ps, reqh("search"))
        End If

        Return ps
    End Function

    Public Overridable Function ShowAction(Optional ByVal form_id As String = "") As Hashtable
        Dim ps As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)
        Dim item As Hashtable = model0.one(id)
        If item.Count = 0 Then Throw New ApplicationException("Not Found")

        'added/updated should be filled before dynamic fields
        ps("add_users_id_name") = fw.model(Of Users).getFullName(item("add_users_id"))
        ps("upd_users_id_name") = fw.model(Of Users).getFullName(item("upd_users_id"))

        'dynamic fields
        ps("fields") = prepareShowFields(item, ps)

        'userlists support
        ps("list_view") = IIf(list_view = "", model0.table_name, list_view)
        ps("mylists") = fw.model(Of UserLists).listForItem(ps("list_view"), id)

        ps("id") = id
        ps("i") = item
        ps("return_url") = return_url
        ps("related_id") = related_id

        Return ps
    End Function

    Public Overridable Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'TODO define via config.json
        'Me.form_new_defaults = New Hashtable 'set new form defaults here if any
        'Me.form_new_defaults = reqh("item") 'OR optionally set defaults from request params
        'item("field")="default value"

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

        ps("fields") = prepareShowFormFields(item, ps)
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
        processSaveShowFormFields(id, fields)

        id = MyBase.modelAddOrUpdate(id, fields)

        processSaveShowFormFieldsAfter(id, fields)

        Return id
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

        'TODO simple validation via showform_fields
        If result AndAlso model0.isExists(item("email"), id) Then
            fw.FERR("email") = "EXISTS"
        End If
        If result AndAlso Not FormUtils.isEmail(item("email")) Then
            fw.FERR("email") = "WRONG"
        End If

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
            {"return_url", Me.return_url},
            {"base_url", Me.base_url}
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

        Return Me.saveCheckResult(True, New Hashtable From {{"ctr", ctr}})
    End Function


    '********************* support for autocomlete related items
    Public Function AutocompleteAction() As Hashtable
        Dim items As ArrayList = model_related.getAutocompleteList(reqs("q"))

        Return New Hashtable From {{"_json", items}}
    End Function

    '********************* support for customizable list screen
    Public Sub UserViewsAction(Optional form_id As String = "")
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

    '''''' HELPERS for dynamic fields

    ''' <summary>
    ''' prepare data for fields repeat in ShowAction based on config.json show_fields parameter
    ''' </summary>
    ''' <param name="item"></param>
    ''' <param name="ps"></param>
    ''' <returns></returns>
    Public Function prepareShowFields(item As Hashtable, ps As Hashtable) As ArrayList
        Dim id = Utils.f2int(item("id"))

        Dim fields As ArrayList = Me.config("show_fields")
        For Each def As Hashtable In fields
            def("i") = item 'ref to item
            def("ps") = ps 'ref to whole ps
            Dim dtype = def("type")
            Dim field = def("field")

            If dtype = "multi" Then
                'complex field
                def("multi_datarow") = fw.model(def("lookup_model")).getMultiList(item(field))

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
                    def("lookup_row") = fw.model(def("lookup_model")).one(item(field))

                    Dim lookup_field = def("lookup_field")
                    If lookup_field = "" Then lookup_field = "iname"

                    def("value") = def("lookup_row")(lookup_field)

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

    Public Function prepareShowFormFields(item As Hashtable, ps As Hashtable) As ArrayList
        Dim id = Utils.f2int(item("id"))

        Dim fields As ArrayList = Me.config("showform_fields")
        For Each def As Hashtable In fields
            def("i") = item 'ref to item
            def("ps") = ps 'ref to whole ps
            Dim dtype = def("type")
            Dim field = def("field")

            If dtype = "multicb" Then
                'complex field
                def("multi_datarow") = fw.model(def("lookup_model")).getMultiList(item(field))
                For Each row As Hashtable In def("multi_datarow") 'contains id, iname, is_checked
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

                    'def("lookup_row") = db.row(def("lookup_table"), New Hashtable From {{lookup_key, item(field)}})
                    'def("value") = def("lookup_row")(lookup_field)

                ElseIf def.ContainsKey("lookup_model") Then 'lookup by model
                    If def.ContainsKey("lookup_field") Then
                        'lookup value
                        def("lookup_row") = fw.model(def("lookup_model")).one(Utils.f2int(item(field)))
                        def("value") = def("lookup_row")(def("lookup_field"))
                    Else
                        'lookup select
                        def("select_options") = fw.model(def("lookup_model")).listSelectOptions()
                        def("value") = item(field)
                    End If

                ElseIf def.ContainsKey("lookup_tpl") Then
                    def("select_options") = FormUtils.selectTplOptions(def("lookup_tpl"), item(field))
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
    Protected Sub processSaveShowFormFields(id As Integer, fields As Hashtable)
        Dim item As Hashtable = reqh("item")

        Dim showform_fields = _fieldsToHash(Me.config("showform_fields"))

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
                End If
            End If
        Next

        'fields("fint") = Utils.f2int(fields("fint")) 'TODO? field accepts only int

    End Sub

    'auto-process fields AFTER record saved to db
    Protected Sub processSaveShowFormFieldsAfter(id As Integer, fields As Hashtable)

        'for now we just look if we have att_links_edit field and update att links
        For Each def As Hashtable In Me.config("showform_fields")
            If def("type") = "att_links_edit" Then
                fw.model(Of Att).updateAttLinks(model0.table_name, id, reqh("att")) 'TODO make att configurable
            End If
        Next

    End Sub

    'convert config's fields list into hashtable as field => {}
    'if there are more than one field - just first field added to the hash
    Protected Function _fieldsToHash(fields As ArrayList) As Hashtable
        Dim result As New Hashtable
        For Each fldinfo As Hashtable In fields
            If Not result.ContainsKey(fldinfo("field")) Then
                result(fldinfo("field")) = fldinfo
            End If
        Next
        Return result
    End Function

End Class
