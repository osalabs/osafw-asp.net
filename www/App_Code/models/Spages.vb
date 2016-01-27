' Static Pages model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class Spages
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "spages"
    End Sub

    'delete record, but don't allow to delete home page
    Public Overrides Sub delete(id As Integer, Optional is_perm As Boolean = False)
        Dim item_old As Hashtable = one(id)
        'home page cannot be deleted
        If item_old("is_home") <> "1" Then
            MyBase.delete(id, is_perm)
        End If
    End Sub

    'retun one latest record by url (i.e. with most recent pub_time if there are more than one page with such url)
    Public Function one_by_url(url As String, parent_id As Integer) As Hashtable
        Dim where As New Hashtable
        where("parent_id") = parent_id
        where("url") = url
        Return db.row(table_name, where, "pub_time desc")
    End Function

    'return one latest record by full_url (i.e. relative url from root, without domain)
    Public Function one_by_full_url(full_url As String) As Hashtable
        Dim url_parts As String() = Split(full_url, "/")
        Dim parent_id As Integer = 0
        Dim item As Hashtable = Nothing
        For i As Integer = 1 To url_parts.GetUpperBound(0)
            item = one_by_url(url_parts(i), parent_id)
            If item.Count = 0 Then
                item = Nothing
                Exit For
            End If
            parent_id = item("id")
        Next
        'item now contains page data for the url
        If item IsNot Nothing AndAlso item.Count > 0 Then
            If item("head_att_id") > "" Then
                'item("head_att_id_url_s") = fw.model(Of Att).get_url_direct(item("head_att_id"), "s")
                'item("head_att_id_url_m") = fw.model(Of Att).get_url_direct(item("head_att_id"), "m")
                item("head_att_id_url") = fw.model(Of Att).get_url_direct(item("head_att_id"))
            End If

        End If

        'page[top_url] used in templates navigation
        If item IsNot Nothing AndAlso url_parts.GetUpperBound(0) >= 1 Then
            item("top_url") = LCase(url_parts(1))
        End If

        Return item
    End Function

    ''' <summary>
    ''' Read ALL rows from db according to where, then apply get_pages_tree to return tree structure 
    ''' </summary>
    ''' <param name="where">where to apply in sql</param>
    ''' <param name="orderby">order by fields to apply in sql</param>
    ''' <returns>parsepage AL with hierarcy (via "children" key)</returns>
    ''' <remarks></remarks>
    Public Function tree(where As String, orderby As String) As ArrayList
        Dim rows As ArrayList = db.array("select * from " & table_name & " where " & where & " order by " & orderby)
        Dim pages_tree As ArrayList = get_pages_tree(rows, 0)
        Return pages_tree
    End Function

    'return parsepage array list of rows with hierarcy (children rows added to parents as "children" key)
    'RECURSIVE!
    Public Function get_pages_tree(rows As ArrayList, parent_id As Integer, Optional level As Integer = 0) As ArrayList
        Dim result As New ArrayList

        For Each row As Hashtable In rows
            If parent_id = row("parent_id") Then
                Dim row2 As Hashtable = row.Clone()
                row2("_level") = level
                'row2("_level1") = level + 1 'to easier use in templates
                row2("children") = get_pages_tree(rows, row("id"), level + 1)
                result.Add(row2)
            End If
        Next

        Return result
    End Function

    ''' <summary>
    ''' Generate parsepage AL of plain list with levelers based on tree structure from get_pages_tree()
    ''' </summary>
    ''' <param name="pages_tree">result of get_pages_tree()</param>
    ''' <param name="level">optional, used in recursive calls</param>
    ''' <returns>parsepage AL with "leveler" array added to each row with level>0</returns>
    ''' <remarks>RECURSIVE</remarks>
    Public Function get_pages_tree_list(pages_tree As ArrayList, Optional level As Integer = 0) As ArrayList
        Dim result As New ArrayList

        If pages_tree IsNot Nothing Then
            For Each row As Hashtable In pages_tree
                result.Add(row)
                'add leveler
                If level > 0 Then
                    Dim leveler As New ArrayList
                    For i As Integer = 1 To level
                        leveler.Add(New Hashtable)
                    Next
                    row("leveler") = leveler
                End If
                'subpages
                result.AddRange(get_pages_tree_list(row("children"), level + 1))
            Next
        End If

        Return result
    End Function

    ''' <summary>
    ''' Generate HTML with options for select with indents for hierarcy
    ''' </summary>
    ''' <param name="selected_id">selected id</param>
    ''' <param name="pages_tree">result of get_pages_tree()</param>
    ''' <param name="level">optional, used in recursive calls</param>
    ''' <returns>HTML with options</returns>
    ''' <remarks>RECURSIVE</remarks>
    Public Function get_pages_tree_select_html(selected_id As String, pages_tree As ArrayList, Optional level As Integer = 0) As String
        Dim result As New StringBuilder
        If pages_tree IsNot Nothing Then
            For Each row As Hashtable In pages_tree

                result.AppendLine("<option value=""" & row("id") & """" & IIf(row("id") = selected_id, " selected=""selected"" ", "") & ">" & Utils.str_repeat("&#8212; ", level) & row("iname") & "</option>")
                'subpages
                result.Append(get_pages_tree_select_html(selected_id, row("children"), level + 1))
            Next
        End If

        Return result.ToString()
    End Function

    ''' <summary>
    ''' Return full url (without domain) for the page item, including url of the page
    ''' </summary>
    ''' <param name="id">record id</param>
    ''' <returns>URL like /page/subpage/subsubpage</returns>
    ''' <remarks></remarks>
    Public Function get_full_url(id As Integer) As String
        If id = 0 Then Return ""

        Dim item As Hashtable = one(id)
        Return get_full_url(Utils.f2int(item("parent_id"))) & "/" & item("url")
    End Function


    'render page by full url
    Public Sub show_page_by_full_url(full_url As String)
        Dim ps As New Hashtable

        Dim item As Hashtable = one_by_full_url(full_url)
        If IsNothing(item) Then
            ps("hide_sidebar") = True
            fw.parser("/error/404", ps)
            Exit Sub
        End If

        ps("page") = item
        ps("hide_sidebar") = True 'TODO - control via item[template]
        fw.parser("/home/spage", ps)
    End Sub



    'check if item exists for a given email
    'Public Overrides Function is_exists(uniq_key As Object, not_id As Integer) As Boolean
    '    Return is_exists_byfield(uniq_key, not_id, "email")
    'End Function

    'return correct url - TODO
    Public Function get_url(id As Integer, icode As String, Optional url As String = Nothing) As String
        If url IsNot Nothing AndAlso url > "" Then
            If Regex.IsMatch(url, "^/") Then url = fw.config("ROOT_URL") & url
            Return url
        Else
            icode = str2icode(icode)
            If icode > "" Then
                Return fw.config("ROOT_URL") & "/Pages/" & icode
            Else
                Return fw.config("ROOT_URL") & "/Pages/" & id
            End If

        End If
    End Function

    ' TODO
    Public Shared Function str2icode(str As String) As String
        str = Trim(str)
        str = Regex.Replace(str, "[^\w ]", " ")
        str = Regex.Replace(str, " +", "-")
        Return str
    End Function

End Class
