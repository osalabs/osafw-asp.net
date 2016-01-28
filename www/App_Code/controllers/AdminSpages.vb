' Static Pages Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminSpagesController
    Inherits FwAdminController
    Protected model As Spages

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of Spages)()
        model = model0

        'initialization
        base_url = "/Admin/Spages"
        required_fields = "iname"
        save_fields = "iname iname_tagline idesc idesc_right head_att_id template prio"

        search_fields = "url iname idesc"
        list_sortdef = "iname asc"   'default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname pub_time|pub_time upd_time|upd_time")
    End Sub

    Public Overrides Function IndexAction() As Hashtable
        'get filters from the search form
        Dim f As Hashtable = Me.get_filter()

        Me.set_list_sorting()

        Me.list_where = " 1=1 "
        Me.set_list_search()
        'set here non-standard search
        If f("status") > "" Then
            Me.list_where &= " and status=" & db.qi(f("status"))
        Else
            Me.list_where &= " and status<>127 " 'by default - show all non-deleted
        End If

        Me.list_count = db.value("select count(*) from " & model.table_name & " where " & Me.list_where)
        If Me.list_count > 0 Then
            'build pages tree
            'TODO - if order not by iname - display plain page list using  Me.get_list_rows()
            Dim pages_tree As ArrayList = model.tree(Me.list_where, "parent_id, prio desc, iname")
            Me.list_rows = model.get_pages_tree_list(pages_tree, 0)

            'apply LIMIT
            If Me.list_count > Me.list_filter("pagesize") Then
                Dim subset As New ArrayList
                Dim start_offset As Integer = Me.list_filter("pagenum") * Me.list_filter("pagesize")

                For i As Integer = start_offset To Math.Min(start_offset + Me.list_filter("pagesize"), Me.list_rows.Count) - 1
                    subset.Add(Me.list_rows.Item(i))
                Next
                Me.list_rows = subset
            End If

            Me.list_pager = FormUtils.get_pager(Me.list_count, Me.list_filter("pagenum"), Me.list_filter("pagesize"))
        Else
            Me.list_rows = New ArrayList
            Me.list_pager = New ArrayList
        End If

        'add/modify rows from db if necessary
        For Each row As Hashtable In Me.list_rows
            row("full_url") = model.get_full_url(row("id"))
        Next

        Dim ps As Hashtable = New Hashtable
        ps("list_rows") = Me.list_rows
        ps("count") = Me.list_count
        ps("pager") = Me.list_pager
        ps("f") = Me.list_filter

        Return ps
    End Function

    Public Overrides Function ShowFormAction(Optional ByVal form_id As String = "") As Hashtable
        'set new form defaults here if any
        If fw.FORM("parent_id") > "" Then
            Me.form_new_defaults = New Hashtable
            Me.form_new_defaults("parent_id") = Utils.f2int(fw.FORM("parent_id"))
        End If
        Dim ps As Hashtable = MyBase.ShowFormAction(form_id)

        Dim item As Hashtable = ps("i")
        Dim where As String = " status<>127 "
        Dim pages_tree As ArrayList = model.tree(where, "parent_id, prio desc, iname")
        ps("select_options_parent_id") = model.get_pages_tree_select_html(item("parent_id"), pages_tree)

        ps("parent_url") = model.get_full_url(Utils.f2int(item("parent_id")))
        ps("full_url") = model.get_full_url(Utils.f2int(item("id")))

        If item("head_att_id") > "" Then
            ps("head_att_id_url_s") = fw.model(Of Att).get_url_direct(item("head_att_id"), "s")
        End If

        Return ps
    End Function

    Public Overrides Sub SaveAction(Optional ByVal form_id As String = "")
        If Me.save_fields Is Nothing Then Throw New Exception("No fields to save defined, define in save_fields ")

        Dim item As Hashtable = req("item")
        Dim id As Integer = Utils.f2int(form_id)

        Try
            Dim item_old As Hashtable = model.one(id)
            'for non-home page enable some fields
            Dim save_fields2 As String = Me.save_fields
            If item_old("is_home") <> "1" Then
                required_fields &= " url"
                save_fields2 &= " parent_id url status pub_time"
            End If

            Validate(id, item)
            'load old record if necessary

            Dim itemdb As Hashtable = FormUtils.form2dbhash(item, save_fields2)
            If Me.save_fields_checkboxes > "" Then FormUtils.form2dbhash_checkboxes(itemdb, item, save_fields_checkboxes)
            itemdb("prio") = Utils.f2int(itemdb("prio"))

            'if no publish time defined - publish it now
            If itemdb("pub_time") = "" Then itemdb("pub_time") = Now()

            id = Me.model_add_or_update(id, itemdb)

            If item_old("is_home") = "1" Then FwCache.remove("home_page") 'reset home page cache if Home page changed

            fw.redirect(base_url & "/" & id & "/edit" & base_url_suffix)
        Catch ex As ApplicationException
            Me.set_form_error(ex)
            fw.route_redirect("ShowForm", New String() {id})
        End Try
    End Sub

End Class
