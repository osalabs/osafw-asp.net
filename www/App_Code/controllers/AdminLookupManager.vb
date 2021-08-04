' LookupManager Admin controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2016 Oleg Savchuk www.osalabs.com

Public Class AdminLookupManagerController
    Inherits FwController
    Public Shared Shadows access_level As Integer = Users.ACL_MANAGER

    Protected model As New LookupManager
    Protected model_tables As New LookupManagerTables
    Protected dict As String 'current lookup dictionary
    Protected defs As Hashtable
    Protected dictionaries_url As String
    Private is_readonly As Boolean = False

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model.init(fw)
        model_tables = fw.model(Of LookupManagerTables)
        required_fields = "" 'default required fields, space-separated
        base_url = "/Admin/LookupManager" 'base url for the controller
        dictionaries_url = base_url & "/(Dictionaries)"

        dict = reqs("d")
        defs = model_tables.oneByTname(dict)
        If defs.Count = 0 Then dict = ""
    End Sub

    Private Sub check_dict()
        If Not dict > "" Then fw.redirect(base_url & "/(Dictionaries)")
        If defs("url") > "" Then fw.redirect(defs("url"))
    End Sub

    Public Function DictionariesAction() As Hashtable
        Dim ps As Hashtable = New Hashtable

        'code below to show list of items in columns instead of plain list

        Dim columns As Integer = 4
        Dim tables As ArrayList = model_tables.list()
        Dim max_rows As Integer = Math.Ceiling(tables.Count / columns)
        Dim cols As New ArrayList

        'add rows
        Dim curcol As Integer = 0
        For Each table As Hashtable In tables
            If cols.Count <= curcol Then cols.Add(New Hashtable)
            Dim h As Hashtable = cols(curcol)
            If h.Count = 0 Then
                h("col_sm") = Math.Floor(12 / columns)
                h("list_rows") = New ArrayList
            End If
            Dim al As ArrayList = h("list_rows")
            al.Add(table)
            If al.Count >= max_rows Then curcol += 1
        Next

        ps("list_сols") = cols
        Return ps
    End Function

    Public Function IndexAction() As Hashtable
        check_dict()

        'if this is one-form dictionary - show edit form with first record
        If defs("is_one_form") = 1 Then
            Dim id_fname As String = fw.model(Of LookupManagerTables).getColumnId(defs)
            Dim row = model.topByTname(defs("tname"))
            'fw.redirect(base_url & "/" & row(id_fname) & "/edit/?d=" & dict)
            Dim args() As [String] = {row(id_fname)}
            fw.routeRedirect("ShowForm", Nothing, args)
            Return Nothing
        End If

        'get columns
        Dim cols As ArrayList = model_tables.getColumns(defs)
        Dim list_table_name As String = defs("tname")
        'logger(defs)
        'logger(cols)

        Dim hf As Hashtable = New Hashtable
        hf("is_two_modes") = True
        Dim f As Hashtable = initFilter("_filter_lookupmanager_" & list_table_name)

        'sorting
        If f("sortby") = "" Then
            If cols.Count > 0 Then
                f("sortby") = cols(0)("name") 'by default - sort by first column
            Else
                f("sortby") = ""
            End If
        End If
        If f("sortdir") <> "desc" Then f("sortdir") = "asc"
        Dim SORTSQL As New Hashtable
        Dim fields_headers As New ArrayList
        Dim group_headers As New ArrayList
        Dim is_group_headers As Boolean = False

        Dim list_cols As New Hashtable
        If defs("list_columns") > "" Then
            list_cols = Utils.commastr2hash(defs("list_columns"))
            hf("is_two_modes") = False 'if custom list defined - don't enable table edit mode
        Else
            'if no custom columns - remove sys cols
            cols = model.filterOutSysCols(cols)
        End If

        For Each col As Hashtable In cols
            SORTSQL(col("name")) = db.q_ident(col("name"))

            If list_cols.Count > 0 AndAlso Not list_cols.ContainsKey(col("name")) Then Continue For

            Dim fh As New Hashtable
            fh("iname") = col("iname")
            fh("colname") = col("name")
            fh("maxlen") = col("maxlen")
            fh("type") = col("itype")
            If fh("type") = "textarea" Then fh("type") = "" 'show textarea as inputtext in table edit mode

            If InStr(col("itype"), ".") > 0 Then
                'lookup type
                fh("type") = "lookup"
                fh("select_options") = model_tables.getLookupSelectOptions(col("itype"), "")
            End If

            fields_headers.Add(fh)

            'detect/build group headers
            Dim igroup As String = Trim(col("igroup"))
            If group_headers.Count = 0 Then
                Dim h As New Hashtable
                h("iname") = igroup
                h("colspan") = 0
                group_headers.Add(h)
            End If
            If igroup = group_headers(group_headers.Count - 1)("iname") Then
                group_headers(group_headers.Count - 1)("colspan") += 1
            Else
                Dim h As New Hashtable
                h("iname") = igroup
                h("colspan") = 1
                group_headers.Add(h)
            End If

            If igroup > "" Then
                is_group_headers = True
            End If
        Next

        Dim where As String = " 1=1"
        If f("s") > "" Then
            Dim slike As String = db.q("%" & f("s") & "%")
            Dim swhere As String = ""
            For Each col As Hashtable In cols
                swhere &= "or " & db.q_ident(col("name")) & " like " & slike
            Next
            If swhere > "" Then where &= " and (0=1 " & swhere & ")"
        End If

        hf("count") = db.value("select count(*) from " & db.q_ident(list_table_name) & " where " & where)
        If hf("count") > 0 Then
            Dim offset As Integer = f("pagenum") * f("pagesize")
            Dim limit As Integer = f("pagesize")
            Dim orderby As String = SORTSQL(f("sortby"))
            If Not orderby > "" Then
                orderby = "1"
                'Throw New Exception("No orderby defined for [" & f("sortby") & "]")
            End If
            If f("sortdir") = "desc" Then
                If InStr(orderby, ",") Then orderby = Replace(orderby, ",", " desc,")
                orderby &= " desc"
            End If

            Dim sql = "SELECT * FROM " & db.q_ident(list_table_name) &
                      " WHERE " & where &
                      " ORDER BY " & orderby &
                      " OFFSET " & offset & " ROWS " &
                      " FETCH NEXT " & limit & " ROWS ONLY"

            hf("list_rows") = db.array(sql)
            hf("pager") = FormUtils.getPager(hf("count"), f("pagenum"), f("pagesize"))
            If Not IsNothing(hf("pager")) Then
                'add dict info for pager
                For Each page As Hashtable In hf("pager")
                    page("d") = dict
                Next
            End If

            'add/modify rows from db
            For Each row As Hashtable In hf("list_rows")
                row("is_readonly") = is_readonly
                'calc md5 first if in edit mode
                If f("mode") = "edit" Then
                    row("row_md5") = model.getRowMD5(row)
                End If

                row("id") = row(model_tables.getColumnId(defs))
                row("d") = dict
                row("f") = f

                Dim fv As New ArrayList
                For Each col As Hashtable In cols
                    If list_cols.Count > 0 AndAlso Not list_cols.ContainsKey(col("name")) Then Continue For

                    Dim fh As New Hashtable
                    fh("colname") = col("name")
                    fh("iname") = col("iname")
                    fh("value") = row(col("name"))
                    If list_cols.Count = 0 AndAlso (col("name") = "status" OrElse col("name") = "iname") Then
                        fh("is_custom") = True
                        'fh("value") = FormUtils.selectTplName("/common/sel/status.sel", row(col("name")))
                    End If


                    fh("id") = row("id")
                    fh("maxlen") = col("maxlen")
                    fh("type") = col("itype")
                    If fh("type") = "textarea" Then fh("type") = "" 'show textarea as inputtext in table edit mode

                    If InStr(col("itype"), ".") > 0 Then
                        'lookup type
                        fh("type") = "lookup"
                        fh("select_options") = model_tables.getLookupSelectOptions(col("itype"), fh("value"))
                        'for lookup type display value should be from lookup table
                        fh("value") = model_tables.getLookupValue(col("itype"), fh("value"))
                    End If

                    fv.Add(fh)
                Next
                row("fields_values") = fv
            Next
        End If
        hf("fields_headers") = fields_headers
        hf("group_headers") = group_headers
        hf("is_group_headers") = is_group_headers
        hf("f") = f
        hf("defs") = defs
        hf("d") = dict
        hf("is_readonly") = is_readonly

        Return hf
    End Function

    Public Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        If is_readonly Then Throw New UserException("Access denied")

        check_dict()

        Dim hf As Hashtable = New Hashtable
        Dim item As Hashtable
        Dim id As Integer = Utils.f2int(form_id)
        Dim cols As ArrayList = model_tables.getColumns(defs)
        Dim is_fwtable As Boolean = False

        If Not defs("list_columns") > "" Then
            'if no custom columns - remove sys cols
            is_fwtable = True
            cols = model.filterOutSysCols(cols)
        End If


        If isGet() Then 'read from db
            If id > 0 Then
                item = model.oneByTname(dict, id)
            Else
                'set defaults here
                item = New Hashtable
                'item("field")='default value'
                item("prio") = model.maxIdByTname(dict) + 1 'default prio (if exists) = max(id)+1 
            End If
        Else
            'read from db
            item = model.oneByTname(dict, id)
            'and merge new values from the form
            Utils.mergeHash(item, reqh("item"))
            'here make additional changes if necessary            
        End If

        Dim fv As New ArrayList
        Dim last_igroup As String = ""
        For Each col As Hashtable In cols
            If is_fwtable AndAlso col("name") = "status" Then Continue For 'for fw tables - status displayed in standard way

            Dim fh As New Hashtable
            fh("colname") = col("name")
            fh("iname") = col("iname")
            fh("value") = item(col("name"))
            fh("type") = Trim(col("itype"))
            If col("maxlen") > "" Then
                If col("maxlen") = "-1" Then
                    fh("maxlen") = "" 'textarea
                    fh("type") = "textarea"
                Else
                    fh("maxlen") = col("maxlen")
                End If
            Else
                fh("maxlen") = col("numeric_precision")
            End If
            If InStr(col("itype"), ".") > 0 Then
                'lookup type
                fh("type") = "lookup"
                fh("select_options") = model_tables.getLookupSelectOptions(col("itype"), fh("value"))
            End If

            Dim igroup As String = Trim(col("igroup"))
            If igroup <> last_igroup Then
                fh("is_group") = True
                fh("igroup") = igroup
                last_igroup = igroup
            End If

            fv.Add(fh)
        Next
        hf("fields") = fv


        'read dropdowns lists from db
        'hf("select_options_parent_id") = FormUtils.select_options_db(db.array("select id, iname from " & model.table_name & " where parent_id=0 and status=0 order by iname"), item("parent_id"))
        'hf("select_options_demo_dicts_id") = fw.model(Of DemoDicts).get_select_options(item("demo_dicts_id"))
        'hf("dict_link_auto_id_iname") = fw.model(Of DemoDicts).iname(item("dict_link_auto_id"))
        'hf("multi_datarow") = fw.model(Of DemoDicts).get_multi_list(item("dict_link_multi"))
        'FormUtils.combo4date(item("fdate_combo"), hf, "fdate_combo")

        hf("is_fwtable") = is_fwtable
        If is_fwtable Then
            hf("add_users_id_name") = fw.model(Of Users).iname(item("add_users_id"))
            hf("upd_users_id_name") = fw.model(Of Users).iname(item("upd_users_id"))
        End If

        hf("id") = id
        hf("i") = item
        hf("defs") = defs
        hf("d") = dict

        Return hf
    End Function

    Public Sub SaveAction(Optional ByVal form_id As String = "")
        If is_readonly Then Throw New UserException("Access denied")

        check_dict()

        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)
        Dim cols As ArrayList = model_tables.getColumns(defs)

        Try
            Validate(id, item)

            Dim itemdb As New Hashtable
            For Each col As Hashtable In cols
                If item.ContainsKey(col("name")) Then
                    itemdb(col("name")) = item(col("name"))
                Else
                    If col("itype") = "checkbox" Then itemdb(col("name")) = 0 'for checkboxes just set them 0
                End If
            Next

            If id > 0 Then
                If model.updateByTname(dict, id, itemdb) Then fw.FLASH("updated", 1)
            Else
                model.addByTname(dict, itemdb)
                fw.FLASH("added", 1)
            End If

            'redirect to list as we don't have id on insert
            'fw.redirect(base_url & "/" & id & "/edit")
            fw.redirect(base_url & "/?d=" & dict)
        Catch ex As ApplicationException
            fw.G("err_msg") = ex.Message
            Dim args() As [String] = {id}
            fw.routeRedirect("ShowForm", Nothing, args)
        End Try
    End Sub

    Public Function Validate(id As String, item As Hashtable) As Boolean
        Dim result As Boolean = True
        result = result And validateRequired(item, Utils.qw(required_fields))
        If Not result Then fw.FERR("REQ") = 1

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
        If is_readonly Then Throw New UserException("Access denied")

        check_dict()

        Dim hf As New Hashtable
        Dim id As Integer = Utils.f2int(form_id)
        Dim item As Hashtable = model.oneByTname(dict, id)
        hf("i") = item
        hf("iname") = item.Values(0)
        hf("id") = id
        hf("defs") = defs
        hf("d") = dict

        Return hf
    End Function

    Public Sub DeleteAction(ByVal form_id As String)
        If is_readonly Then Throw New UserException("Access denied")

        check_dict()
        Dim id As Integer = Utils.f2int(form_id)

        model.deleteByTname(dict, id)
        fw.FLASH("onedelete", 1)
        fw.redirect(base_url & "/?d=" & dict)
    End Sub

    Public Sub SaveMultiAction()
        If is_readonly Then Throw New UserException("Access denied")

        check_dict()

        Try
            Dim del_ctr As Integer = 0
            Dim cbses As Hashtable = reqh("cb")
            If cbses Is Nothing Then cbses = New Hashtable
            If cbses.Count > 0 Then
                'multirecord delete
                For Each id As String In cbses.Keys
                    If fw.FORM.ContainsKey("delete") Then
                        model.deleteByTname(dict, id)
                        del_ctr += 1
                    End If
                Next
            End If

            If reqs("mode") = "edit" Then
                'multirecord save
                Dim cols As ArrayList = model_tables.getColumns(defs)

                'go thru all existing rows
                Dim rows As Hashtable = reqh("row")
                If rows Is Nothing Then rows = New Hashtable
                Dim rowsdel As Hashtable = reqh("del")
                If rowsdel Is Nothing Then rowsdel = New Hashtable
                Dim ids_md5 As New Hashtable
                For Each key As String In rows.Keys
                    Dim form_id As String = key
                    Dim id As Integer = Utils.f2int(form_id)
                    If id = 0 Then Continue For 'skip wrong rows

                    Dim md5 As String = rows(key)
                    'logger(form_id)
                    Dim item As Hashtable = reqh("f" & form_id)
                    Dim itemdb As New Hashtable
                    'copy from form item to db item - only defined columns
                    For Each col As Hashtable In cols
                        If item.ContainsKey(col("name")) Then
                            itemdb(col("name")) = item(col("name"))
                        End If
                    Next
                    'check if this row need to be deleted
                    If rowsdel.ContainsKey(form_id) Then
                        model.deleteByTname(dict, id)
                        del_ctr += 1
                    Else
                        'existing row
                        model.updateByTname(dict, id, itemdb, md5)
                        fw.FLASH("updated", 1)
                    End If
                Next

                'new rows
                rows = reqh("new")
                For Each key As String In rows.Keys
                    Dim form_id As String = key
                    Dim id As Integer = Utils.f2int(form_id)
                    If id = 0 Then Continue For 'skip wrong rows
                    'logger("new formid=" & form_id)

                    Dim item As Hashtable = reqh("fnew" & form_id)
                    Dim itemdb As New Hashtable
                    Dim is_row_empty As Boolean = True
                    'copy from form item to db item - only defined columns
                    For Each col As Hashtable In cols
                        If item.ContainsKey(col("name")) Then
                            itemdb(col("name")) = item(col("name"))
                            If item(col("name")) > "" Then is_row_empty = False 'detect at least one non-empty value
                        End If
                    Next

                    'add new row, but only if at least one value is not empty
                    If Not is_row_empty Then
                        model.addByTname(dict, itemdb)
                        fw.FLASH("updated", 1)
                    End If
                Next
            End If

            If del_ctr > 0 Then
                fw.FLASH("multidelete", del_ctr)
            End If

            fw.redirect(base_url & "/?d=" & dict)
        Catch ex As Exception
            Throw
            fw.G("err_msg") = ex.Message
            fw.routeRedirect("Index")
        End Try

    End Sub

    'TODO for lookup tables
    Public Function AutocompleteAction() As Hashtable
        Dim items As ArrayList = model_tables.getAutocompleteList(reqs("q"))

        Return New Hashtable From {{"_json", items}}
    End Function
End Class
