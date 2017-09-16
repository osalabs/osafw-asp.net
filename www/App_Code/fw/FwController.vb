' Fw Controller base class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2017 Oleg Savchuk www.osalabs.com

Public MustInherit Class FwController
    Public Shared route_default_action As String = "" 'supported values - "" (use Default Parser for unknown actions), index (use IndexAction for unknown actions), show (assume action is id and use ShowAction)
    Public base_url As String 'base url for the controller

    Public list_sortdef As String       'required, default list sorting: name asc|desc
    Public list_sortmap As Hashtable    'required, sortmap fields
    Public search_fields As String      'optional, search fields, space-separated 
    'fields to search via $s=list_filter("s"), ! - means exact match, not "like"
    'format: "field1 field2,!field3 field4" => field1 LIKE '%$s%' or (field2 LIKE '%$s%' and field3='$s') or field4 LIKE '%$s%'

    Public form_new_defaults As Hashtable   'optional, defaults for the fields in new form
    Public required_fields As String        'optional, default required fields, space-separated
    Public save_fields As String            'required, fields to save from the form to db, space-separated
    Public save_fields_checkboxes As String 'optional, checkboxes fields to save from the form to db, qw string: "field|def_value field2|def_value2"

    Protected fw As FW
    Protected db As DB
    Protected model0 As FwModel
    Protected list_view As String                  ' table/view to use in list sql, if empty model0.table_name used
    Protected list_orderby As String               ' orderby for the list screen
    Protected list_filter As Hashtable             ' filter values for the list screen
    Protected list_where As String = " status<>127 " ' where to use in list sql, default is status=0
    Protected list_count As Integer                ' count of list rows returned from db
    Protected list_rows As ArrayList               ' list rows returned from db (array of hashes)
    Protected list_pager As ArrayList              ' pager for the list from FormUtils.get_pager
    Protected return_url As String                 ' url to return after SaveAction successfully completed, passed via request
    Protected related_id As String                 ' related id, passed via request. Controller should limit view to items related to this id
    Protected related_field_name As String         ' if set (in Controller) and $related_id passed - list will be filtered on this field

    Public Sub New(Optional fw As FW = Nothing)
        If fw IsNot Nothing Then
            Me.fw = fw
            Me.db = fw.db
        End If
    End Sub

    Public Overridable Sub init(fw As FW)
        Me.fw = fw
        Me.db = fw.db

        return_url = reqs("return_url")
        related_id = reqs("related_id")
    End Sub

    'set of helper functions to return string, integer, date values from request (fw.FORM)
    Public Function req(iname As String) As Object
        Return fw.FORM(iname)
    End Function
    Public Function reqh(iname As String) As Hashtable
        If fw.FORM(iname) IsNot Nothing AndAlso fw.FORM(iname).GetType() Is GetType(Hashtable) Then
            Return fw.FORM(iname)
        Else
            Return New Hashtable
        End If
    End Function

    Public Function reqs(iname As String) As String
        Dim value As String = fw.FORM(iname)
        If IsNothing(value) Then value = ""
        Return value
    End Function
    Public Function reqi(iname As String) As Integer
        Return Utils.f2int(fw.FORM(iname))
    End Function
    Public Function reqd(iname As String) As Object
        Return Utils.f2date(fw.FORM(iname))
    End Function

    Public Sub rw(ByVal str As String)
        fw.resp.Write(str)
        fw.resp.Write("<br>" & vbCrLf)
    End Sub

    '----------------- just a covenience methods
    Public Sub logger(ByRef dmp_obj As Object)
        fw.logger("DEBUG", dmp_obj)
    End Sub

    'return hashtable of filter values
    'NOTE: automatically set to defaults - pagenum=0 and pagesize=MAX_PAGE_ITEMS
    'NOTE: if request param 'dofilter' passed - session filters cleaned
    'sample in IndexAction: me.get_filter()
    Public Overridable Function initFilter(Optional session_key As String = Nothing) As Hashtable
        Dim f As Hashtable = fw.FORM("f")
        If f Is Nothing Then f = New Hashtable

        If IsNothing(session_key) Then session_key = "_filter_" & fw.G("controller.action")

        Dim sfilter As Hashtable = fw.SESSION(session_key)
        If sfilter Is Nothing OrElse Not (TypeOf sfilter Is Hashtable) Then sfilter = New Hashtable

        'if not forced filter - merge form filters to session filters
        Dim is_dofilter As Boolean = fw.FORM.ContainsKey("dofilter")
        If Not is_dofilter Then
            Utils.mergeHash(sfilter, f)
            f = sfilter
        End If

        'paging
        If Not f.ContainsKey("pagenum") OrElse Not Regex.IsMatch(f("pagenum"), "^\d+$") Then f("pagenum") = 0
        If Not f.ContainsKey("pagesize") OrElse Not Regex.IsMatch(f("pagesize"), "^\d+$") Then f("pagesize") = fw.config("MAX_PAGE_ITEMS")

        'save in session for later use
        fw.SESSION(session_key, f)

        Me.list_filter = f
        Return f
    End Function

    ''' <summary>
    ''' Validate required fields are non-empty and set global fw.ERR[field] values in case of errors
    ''' </summary>
    ''' <param name="item">fields/values to validate</param>
    ''' <param name="fields">field names required to be non-empty (trim used)</param>
    ''' <returns>true if all required field names non-empty</returns>
    ''' <remarks>also set global fw.ERR[REQUIRED]=true in case of validation error</remarks>
    Public Overloads Function validateRequired(item As Hashtable, fields As Array) As Boolean
        Dim result As Boolean = True
        If item IsNot Nothing AndAlso IsArray(fields) AndAlso fields.Length > 0 Then
            For Each fld As String In fields
                If fld > "" AndAlso (Not item.ContainsKey(fld) OrElse Trim(item(fld)) = "") Then
                    result = False
                    fw.FERR(fld) = True
                End If
            Next
        Else
            result = False
        End If
        If Not result Then fw.FERR("REQUIRED") = True
        Return result
    End Function
    'same as above but fields param passed as a qw string
    Public Overloads Function validateRequired(item As Hashtable, fields As String) As Boolean
        Return validateRequired(item, Utils.qw(fields))
    End Function

    ''' <summary>
    ''' Check validation result (validate_required)
    ''' </summary>
    ''' <param name="result">to use from external validation check</param>
    ''' <remarks>throw ValidationException exception if global ERR non-empty.
    ''' Also set global ERR[INVALID] if ERR non-empty, but ERR[REQUIRED] not true
    ''' </remarks>
    Public Sub validateCheckResult(Optional result As Boolean = True)
        If fw.FERR.ContainsKey("REQUIRED") AndAlso fw.FERR("REQUIRED") Then
            result = False
        End If

        If fw.FERR.Count > 0 AndAlso (Not fw.FERR.ContainsKey("REQUIRED") OrElse Not fw.FERR("REQUIRED")) Then
            fw.FERR("INVALID") = True
            result = False
        End If

        If Not result Then Throw New ValidationException()
    End Sub

    ''' <summary>
    ''' Set list sorting fields - Me.list_orderby according to Me.list_filter filter and Me.list_sortmap and Me.list_sortdef
    ''' </summary>
    ''' <remarks></remarks>
    Public Overridable Sub setListSorting()
        If Me.list_sortdef Is Nothing Then Throw New Exception("No default sort order defined, define in list_sortdef ")
        If Me.list_sortmap Is Nothing Then Throw New Exception("No sort order mapping defined, define in list_sortmap ")

        Dim sortdef_field As String = Nothing
        Dim sortdef_dir As String = Nothing
        Utils.split2(" ", Me.list_sortdef, sortdef_field, sortdef_dir)

        If Me.list_filter("sortby") = "" Then Me.list_filter("sortby") = sortdef_field
        If Me.list_filter("sortdir") <> "desc" AndAlso Me.list_filter("sortdir") <> "asc" Then Me.list_filter("sortdir") = sortdef_dir

        Dim orderby As String = Trim(Me.list_sortmap(Me.list_filter("sortby")))
        If Not orderby > "" Then Throw New Exception("No orderby defined for [" & Me.list_filter("sortby") & "], define in list_sortmap")

        If Me.list_filter("sortdir") = "desc" Then
            'if sortdir is desc, i.e. opposite to default - invert order for orderby fields
            'go thru each order field
            Dim aorderby As String() = Split(orderby, ",")
            For i As Integer = 0 To aorderby.Length - 1
                Dim field As String = Nothing, order As String = Nothing
                Utils.split2("\s+", Trim(aorderby(i)), field, order)

                If order = "desc" Then
                    order = "asc"
                Else
                    order = "desc"
                End If
                aorderby(i) = field & " " & order
            Next
            orderby = Join(aorderby, ", ")

        End If
        Me.list_orderby = orderby
    End Sub

    ''' <summary>
    ''' Add to Me.list_where search conditions from Me.list_filter("s") and based on fields in Me.search_fields
    ''' </summary>
    ''' <remarks>Sample: Me.search_fields="field1 field2,!field3 field4" => field1 LIKE '%$s%' or (field2 LIKE '%$s%' and field3='$s') or field4 LIKE '%$s%'</remarks>
    Public Overridable Sub setListSearch()
        'Me.list_where = " status = 0" 'if initial where empty, use " 1=1 "

        Dim s As String = Trim(Me.list_filter("s"))
        If s > "" AndAlso Me.search_fields > "" Then
            Dim list_table_name As String = list_view
            If list_table_name = "" Then list_table_name = model0.table_name

            Dim like_quoted As String = db.q("%" & s & "%")

            Dim afields As String() = Utils.qw(Me.search_fields) 'OR fields delimited by space
            For i As Integer = 0 To afields.Length - 1
                Dim afieldsand As String() = Split(afields(i), ",") 'AND fields delimited by comma

                For j As Integer = 0 To afieldsand.Length - 1
                    Dim fand As String = afieldsand(j)
                    If fand.Substring(0, 1) = "!" Then
                        'exact match
                        fand = fand.Substring(1)
                        afieldsand(j) = fand & " = " & db.qone(list_table_name, fand, s)
                    Else
                        'like match
                        afieldsand(j) = fand & " LIKE " & like_quoted
                    End If
                Next
                afields(i) = Join(afieldsand, " and ")
            Next
            list_where &= " and (" & Join(afields, " or ") & ")"
        End If

        If related_id > "" AndAlso related_field_name > "" Then
            list_where &= " and " & db.q_ident(related_field_name) & "=" & db.q(related_id)
        End If
    End Sub

    ''' <summary>
    ''' Perform 2 queries to get list of rows.
    ''' Set variables:
    ''' Me.list_count - count of rows obtained from db
    ''' Me.list_rows list of rows
    ''' Me.list_pager pager from FormUtils.get_pager
    ''' </summary>
    ''' <remarks></remarks>
    Public Overridable Sub getListRows()
        Dim list_table_name As String = list_view
        If list_table_name = "" Then list_table_name = model0.table_name
        Me.list_count = db.value("select count(*) from " & list_table_name & " where " & Me.list_where)
        If Me.list_count > 0 Then
            Dim offset As Integer = Me.list_filter("pagenum") * Me.list_filter("pagesize")
            Dim limit As Integer = Me.list_filter("pagesize")

            'offset+1 because _RowNumber starts from 1
            Dim sql As String = "SELECT * FROM (" &
                            "   SELECT *, ROW_NUMBER() OVER (ORDER BY " & Me.list_orderby & ") AS _RowNumber" &
                            "   FROM " & list_table_name &
                            "   WHERE " & Me.list_where &
                            ") tmp WHERE _RowNumber BETWEEN " & (offset + 1) & " AND " & (offset + 1 + limit - 1)
            'for MySQL this would be much simplier
            'sql = "SELECT * FROM model0.table_name WHERE Me.list_where ORDER BY Me.list_orderby LIMIT offset, limit";

            Me.list_rows = db.array(sql)
            Me.list_pager = FormUtils.getPager(Me.list_count, Me.list_filter("pagenum"), Me.list_filter("pagesize"))
        Else
            Me.list_rows = New ArrayList
            Me.list_pager = New ArrayList
        End If

        If related_id > "" Then
            Utils.arrayInject(list_rows, New Hashtable From {{"related_id", related_id}})
        End If

        'add/modify rows from db - use in override child class
        'For Each row As Hashtable In list_rows
        '    row("field") = "value"
        'Next

    End Sub

    Public Sub setFormError(ex As Exception)
        'if Validation exception - don't set general error message - specific validation message set in templates
        If Not (TypeOf ex Is ValidationException) Then
            fw.G("err_msg") = ex.Message
        End If
    End Sub

    ''' <summary>
    ''' Add or update records in db (Me.model0)
    ''' </summary>
    ''' <param name="id">id of the record, 0 if add</param>
    ''' <param name="fields">hash of field/values</param>
    ''' <returns>new autoincrement id (if added) or old id (if update)</returns>
    ''' <remarks>Also set fw.FLASH</remarks>
    Public Overridable Function modelAddOrUpdate(id As Integer, fields As Hashtable) As Integer
        If id > 0 Then
            model0.update(id, fields)
            fw.FLASH("record_updated", 1)
        Else
            id = model0.add(fields)
            fw.FLASH("record_added", 1)
        End If
        Return id
    End Function

    Public Overridable Function getReturnLocation(Optional id As String = "") As String
        Dim result = ""
        Dim url As String

        If id > "" Then
            url = Me.base_url & "/" & id & "/edit"
        Else
            url = Me.base_url
        End If

        If return_url > "" Then
            If fw.isJsonExpected() Then
                'if json - it's usually autosave - don't redirect back to return url yet
                result = url & "?return_url=" & Utils.urlescape(return_url) & IIf(related_id > "", "&related_id=" & related_id, "")
            Else
                result = return_url
            End If
        Else
            result = url & IIf(related_id > "", "?related_id=" & related_id, "")
        End If

        Return result
    End Function

    ''' <summary>
    ''' Called from SaveAction/DeleteAction/DeleteMulti or similar. Return json or route redirect back to ShowForm or redirect to proper location
    ''' </summary>
    ''' <param name="success">operation successful or not</param>
    ''' <param name="id">item id</param>
    ''' <param name="is_new">true if it's newly added item</param>
    ''' <param name="action">route redirect to this method if error</param>
    ''' <param name="location">redirect to this location if success</param>
    ''' <param name="more_json">added to json response</param>
    ''' <returns></returns>
    Public Overridable Overloads Function saveCheckResult(success As Boolean, Optional id As String = "", Optional is_new As Boolean = False, Optional action As String = "ShowForm", Optional location As String = "", Optional more_json As Hashtable = Nothing) As Hashtable
        If location = "" Then location = Me.getReturnLocation(id)

        If fw.isJsonExpected() Then
            Dim ps = New Hashtable From {
                {"_json", True},
                {"success", success},
                {"id", id},
                {"is_new", is_new},
                {"location", location},
                {"err_msg", fw.G("err_msg")}
            }
            'TODO add ERR field errors to response if any
            If Not IsNothing(more_json) Then Utils.mergeHash(ps, more_json)

            Return ps
        Else
            'If save Then success - Return redirect
            'If save Then failed - Return back To add/edit form
            If success Then
                fw.redirect(location)
            Else
                fw.routeRedirect(action, New String() {id})
            End If
        End If
        Return Nothing
    End Function

    Public Overridable Overloads Function saveCheckResult(success As Boolean, more_json As Hashtable) As Hashtable
        Return saveCheckResult(success, "", False, "no_action", "", more_json)
    End Function

    '''''''''''''''''''''''''''''''''''''' Default Actions
    'Public Function IndexAction() As Hashtable
    '    logger("in Base controller IndexAction")
    '    Return New Hashtable
    'End Function

End Class

