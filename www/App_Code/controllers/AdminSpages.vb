' Static Pages Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminSpagesController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 80

    Protected model As Spages

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of Spages)()
        model = model0

        'initialization
        base_url = "/Admin/Spages"
        required_fields = "iname"
        save_fields = "iname idesc idesc_left idesc_right head_att_id template prio meta_keywords meta_description custom_css custom_js redirect_url layout"

        search_fields = "url iname idesc"
        list_sortdef = "iname asc"   'default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname pub_time|pub_time upd_time|upd_time status|status url|url")
    End Sub

    Public Overrides Function IndexAction() As Hashtable
        'get filters from the search form
        Dim f As Hashtable = Me.initFilter()

        Me.setListSorting()
        Me.setListSearch()
        Me.setListSearchStatus()

        If list_filter("sortby") = "iname" AndAlso list_filter("s") = "" And (Me.list_filter("status") = "" OrElse Me.list_filter("status") = "0") Then
            'show tree only if sort by title and no search and status by all or active
            Me.list_count = db.value("select count(*) from " & model.table_name & " where " & Me.list_where)
            If Me.list_count > 0 Then
                'build pages tree
                Dim pages_tree As ArrayList = model.tree(Me.list_where, "parent_id, prio desc, iname")
                Me.list_rows = model.getPagesTreeList(pages_tree, 0)

                'apply LIMIT
                If Me.list_count > Me.list_filter("pagesize") Then
                    Dim subset As New ArrayList
                    Dim start_offset As Integer = Me.list_filter("pagenum") * Me.list_filter("pagesize")

                    For i As Integer = start_offset To Math.Min(start_offset + Me.list_filter("pagesize"), Me.list_rows.Count) - 1
                        subset.Add(Me.list_rows.Item(i))
                    Next
                    Me.list_rows = subset
                End If

                Me.list_pager = FormUtils.getPager(Me.list_count, Me.list_filter("pagenum"), Me.list_filter("pagesize"))
            Else
                Me.list_rows = New ArrayList
                Me.list_pager = New ArrayList
            End If
        Else
            'if order not by iname or search performed - display plain page list using  Me.get_list_rows()
            Me.getListRows()
        End If

        'add/modify rows from db if necessary
        For Each row As Hashtable In Me.list_rows
            row("full_url") = model.getFullUrl(row("id"))
        Next

        Dim ps = Me.setPS()

        Return ps
    End Function

    Public Overrides Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'set new form defaults here if any
        If reqs("parent_id") > "" Then
            Me.form_new_defaults = New Hashtable
            Me.form_new_defaults("parent_id") = reqi("parent_id")
        End If
        Dim ps As Hashtable = MyBase.ShowFormAction(form_id)

        Dim item As Hashtable = ps("i")
        Dim id As Integer = item("id")
        Dim where As String = " status<>127 "
        Dim pages_tree As ArrayList = model.tree(where, "parent_id, prio desc, iname")
        ps("select_options_parent_id") = model.getPagesTreeSelectHtml(item("parent_id"), pages_tree)

        ps("parent_url") = model.getFullUrl(Utils.f2int(item("parent_id")))
        ps("full_url") = model.getFullUrl(Utils.f2int(item("id")))

        ps("parent") = model.one(Utils.f2int(item("parent_id")))

        If item("head_att_id") > "" Then
            ps("att") = fw.model(Of Att).one(Utils.f2int(item("head_att_id")))
        End If

        If id > 0 Then
            ps("subpages") = model.listChildren(id)
        End If

        ps("layouts_select") = New ArrayList()
        For Each key As String In fw.config.Keys()
            If key.IndexOf("PAGE_LAYOUT") = 0 Then
                ps("layouts_select").Add(DB.h("id", fw.config(key), "iname", key))
            End If
        Next

        Return ps
    End Function

    Public Overrides Function SaveAction(Optional ByVal form_id As String = "") As Hashtable
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

        Dim item As Hashtable = reqh("item")
        Dim id As Integer = Utils.f2int(form_id)
        Dim success = True
        Dim is_new = (id = 0)

        Try
            Dim item_old As Hashtable = model.one(id)
            'for non-home page enable some fields
            Dim save_fields2 As String = Me.save_fields
            If item_old("is_home") <> "1" Then
                save_fields2 &= " parent_id url status pub_time"
            End If

            'auto-generate url if it's empty
            If item("url") = "" Then
                item("url") = item("iname")
                item("url") = Regex.Replace(item("url"), "^\W+", "")
                item("url") = Regex.Replace(item("url"), "\W+$", "")
                item("url") = Regex.Replace(item("url"), "\W+", "-")
                If item("url") = "" Then
                    If id > 0 Then
                        item("url") = "page-" & id
                    Else
                        item("url") = "page-" & Utils.uuid()
                    End If
                End If
            End If

            Validate(id, item)
            'load old record if necessary

            Dim itemdb As Hashtable = FormUtils.filter(item, save_fields2)
            If Me.save_fields_checkboxes > "" Then FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes)
            itemdb("prio") = Utils.f2int(itemdb("prio"))

                'if no publish time defined - publish it now
                If itemdb("pub_time") = "" Then itemdb("pub_time") = Now()

            id = Me.modelAddOrUpdate(id, itemdb)

            If item_old("is_home") = "1" Then FwCache.remove("home_page") 'reset home page cache if Home page changed

        Catch ex As ApplicationException
            success = False
            Me.setFormError(ex)
        End Try

        Return Me.afterSave(success, id, is_new)
    End Function

    Public Overrides Sub Validate(id As Integer, item As Hashtable)
        Dim result As Boolean = Me.validateRequired(item, Me.required_fields)


        If result AndAlso model.isExistsByUrl(item("url"), Utils.f2int(item("parent_id")), id) Then
            fw.FERR("url") = "EXISTS"
        End If

        'If result AndAlso model0.isExists(item("iname"), id) Then
        '    fw.FERR("iname") = "EXISTS"
        'End If

        'If result AndAlso Not SomeOtherValidation() Then
        '    FW.FERR("other field name") = "HINT_ERR_CODE"
        'End If

        Me.validateCheckResult()
    End Sub

End Class
