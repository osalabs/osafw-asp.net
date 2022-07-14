' Form processing framework utils
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Utils

Public Class FormUtils
    Public Shared Function getYesNo() As Array
        Return qw("No|No Yes|Yes")
    End Function

    Public Shared Function getYN() As Array
        Return qw("N|No Y|Yes")
    End Function

    Public Shared Function getStates() As Array
        Return qw("AL|Alabama AK|Alaska AZ|Arizona AR|Arkansas CA|California CO|Colorado CT|Connecticut DE|Delaware DC|District&nbsp;of&nbsp;Columbia FL|Florida GA|Georgia HI|Hawaii ID|Idaho IL|Illinois IN|Indiana IA|Iowa KS|Kansas KY|Kentucky LA|Louisiana ME|Maine MD|Maryland MA|Massachusetts MI|Michigan MN|Minnesota MS|Mississippi MO|Missouri MT|Montana NE|Nebraska NV|Nevada NH|New&nbsp;Hampshire NJ|New&nbsp;Jersey NM|New&nbsp;Mexico NY|New&nbsp;York NC|North&nbsp;Carolina ND|North&nbsp;Dakota OH|Ohio OK|Oklahoma OR|Oregon PA|Pennsylvania RI|Rhode&nbsp;Island SC|South&nbsp;Carolina SD|South&nbsp;Dakota TN|Tennessee TX|Texas UT|Utah VT|Vermont VA|Virginia WA|Washington WV|West&nbsp;Virgina WI|Wisconsin WY|Wyoming")
    End Function

    'return radio inputs
    ' arr can contain strings or strings with separator "|" for value/text ex: Jan|January,Feb|February
    ' separator - what to put after each radio (for ex - "<br>")
    Public Shared Function radioOptions(ByVal iname As String, ByVal arr As Array, ByVal isel As String, Optional ByVal separator As String = "") As String
        Dim result As New StringBuilder

        isel = Trim(isel)

        Dim i As Integer, av() As String, val As String, text As String
        For i = LBound(arr) To UBound(arr)
            If (InStr(arr(i), "|")) Then
                av = Split(arr(i), "|")
                val = av(0)
                text = av(1)
            Else
                val = arr(i)
                text = arr(i)
            End If

            result.Append("<label><input type=""radio"" name=""" & iname & """ id=""" & iname & i & """ value=""" & val & """")
            If isel = Trim(val) Then
                result.Append(" checked ")
            End If
            result.Append(">" & text & "</label>" & separator & vbCrLf)
        Next

        Return result.ToString()
    End Function

    ' arr is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
    ' "id" key is optional, if not present - iname will be used for values too
    ' isel may contain multiple comma-separated values
    Public Shared Function selectOptions(ByVal arr As ArrayList, ByVal isel As String, Optional is_multi As Boolean = False) As String
        If isel Is Nothing Then isel = ""

        Dim asel() As String
        If is_multi Then
            asel = Split(isel, ",")
        Else
            ReDim asel(0)
            asel(0) = isel
        End If

        Dim i As Integer
        'trim all elements, so it would be simplier to compare
        For i = LBound(asel) To UBound(asel)
            asel(i) = Trim(asel(i))
        Next

        Dim val As String, text As String, result As New StringBuilder
        For Each item As Hashtable In arr
            text = Utils.htmlescape(item("iname"))
            If item.ContainsKey("id") Then
                val = item("id")
            Else
                val = item("iname")
            End If

            result.Append("<option value=""").Append(Utils.htmlescape(val)).Append("""")
            If item.ContainsKey("class") Then result.Append(" class=""" & item("class") & """")
            If Array.IndexOf(asel, Trim(val)) <> -1 Then
                result.Append(" selected ")
            End If
            result.Append(">").Append(text).Append("</option>" & vbCrLf)
        Next

        Return result.ToString()
    End Function

    ''' <summary>
    ''' get name for the value fromt the select template
    ''' ex: selectTplName('/common/sel/status.sel', 127) => 'Deleted'
    ''' TODO: refactor to make common code with ParsePage?
    ''' </summary>
    ''' <param name="tpl_path"></param>
    ''' <param name="sel_id"></param>
    ''' <returns></returns>
    Public Shared Function selectTplName(ByVal tpl_path As String, ByVal sel_id As String) As String
        Dim result As String = ""
        If sel_id Is Nothing Then sel_id = ""

        Dim lines As String() = FW.get_file_lines(FwConfig.settings("template") + tpl_path)

        Dim line As String
        For Each line In lines
            If line.Length < 2 Then Continue For

            Dim arr() As String = Split(line, "|", 2)
            Dim value As String = arr(0)
            Dim desc As String = arr(1)

            If desc.Length < 1 Or value <> sel_id Then Continue For

            'result = ParsePage.RX_LANG.Replace(desc, "$1")
            result = New Regex("`(.+?)`", RegexOptions.Compiled).Replace(desc, "$1")
            Exit For
        Next

        Return result
    End Function

    Public Shared Function selectTplOptions(ByVal tpl_path As String) As ArrayList
        Dim result As New ArrayList

        Dim lines As String() = FW.get_file_lines(FwConfig.settings("template") + tpl_path)

        For Each line In lines
            If line.Length < 2 Then Continue For

            Dim arr() As String = Split(line, "|", 2)
            Dim value As String = arr(0)
            Dim desc As String = arr(1)

            'desc = ParsePage.RX_LANG.Replace(desc, "$1")
            desc = New Regex("`(.+?)`", RegexOptions.Compiled).Replace(desc, "$1")
            result.Add(New Hashtable From {{"id", value}, {"iname", desc}})
        Next

        Return result
    End Function

    Public Shared Function cleanInput(ByVal strIn As String) As String
        ' Replace invalid characters with empty strings.
        Return Regex.Replace(strIn, "[^\w\.\,\:\\\%@\-\/ ]", "")
    End Function

    '********************************* validators
    Public Shared Function isEmail(ByVal email As String) As Boolean
        Dim re As String = "^[\w\.\-\+\=]+\@(?:\w[\w-]*\.?){1,4}[a-zA-Z]{2,16}$"
        Return Regex.IsMatch(email, re)
    End Function

    'validate phones in forms:
    ' (xxx) xxx-xxxx
    ' xxx xxx xx xx
    ' xxx-xxx-xx-xx
    ' xxxxxxxxxx
    Public Shared Function isPhone(ByVal phone As String) As Boolean
        Dim re As String = "^\(?\d{3}\)?[\- ]?\d{3}[\- ]?\d{2}[\- ]?\d{2}$"
        Return Regex.IsMatch(phone, re)
    End Function

    'return pager or Nothing if no paging required
    Public Shared Function getPager(count As Integer, pagenum As Integer, Optional pagesize As Object = Nothing) As ArrayList
        If pagesize Is Nothing Then pagesize = 25 'TODO get from  FW.config("MAX_PAGE_ITEMS")
        Dim pager As ArrayList = Nothing
        Const PAD_PAGES = 5

        If count > pagesize Then
            pager = New ArrayList
            Dim page_count As Integer = Math.Ceiling(count / pagesize)

            Dim from_page = pagenum - PAD_PAGES
            If from_page < 0 Then from_page = 0

            Dim to_page = pagenum + PAD_PAGES
            If to_page > page_count - 1 Then to_page = page_count - 1

            For i As Integer = from_page To to_page
                Dim pager_item As New Hashtable
                If pagenum = i Then pager_item("is_cur_page") = 1
                pager_item("pagenum") = i
                pager_item("pagenum_show") = i + 1
                If i = from_page Then
                    If pagenum > PAD_PAGES Then pager_item("is_show_first") = True
                    If pagenum > 0 Then
                        pager_item("is_show_prev") = True
                        pager_item("pagenum_prev") = pagenum - 1
                    End If
                ElseIf i = to_page Then
                    If pagenum < page_count - 1 Then
                        pager_item("is_show_next") = True
                        pager_item("pagenum_next") = pagenum + 1
                    End If
                End If

                pager.Add(pager_item)
            Next i
        End If

        Return pager
    End Function

    'if is_exists (default true) - only values actually exists in input hash returned
    Public Overloads Shared Function filter(item As Hashtable, fields As Array, Optional is_exists As Boolean = True) As Hashtable
        Dim result As New Hashtable
        If item IsNot Nothing Then
            For Each fld As String In fields
                If fld IsNot Nothing AndAlso (Not is_exists OrElse item.ContainsKey(fld)) Then result(fld) = item(fld)
            Next
        End If
        Return result
    End Function
    'save as above but fields can be passed as qw string
    Public Overloads Shared Function filter(item As Hashtable, fields As String, Optional is_exists As Boolean = True) As Hashtable
        Return filter(item, Utils.qw(fields), is_exists)
    End Function

    'similar to form2dbhash, but for checkboxes (as unchecked checkboxes doesn't passed from form)
    'RETURN: by ref itemdb - add fields with default_value or form value
    Public Overloads Shared Function filterCheckboxes(itemdb As Hashtable, item As Hashtable, fields As Array, Optional default_value As String = "0") As Boolean
        If item IsNot Nothing Then
            For Each fld As String In fields
                If item.ContainsKey(fld) Then
                    itemdb(fld) = item(fld)
                Else
                    itemdb(fld) = default_value
                End If
            Next
        End If
        Return True
    End Function
    'same as above, but fields is qw string with default values: "field|def_value field2|def_value2"
    'default value = "0"
    Public Overloads Shared Function filterCheckboxes(itemdb As Hashtable, item As Hashtable, fields As String) As Boolean
        If item IsNot Nothing Then
            Dim hfields As Hashtable = Utils.qh(fields, "0")
            For Each fld As String In hfields.Keys
                If item.ContainsKey(fld) Then
                    itemdb(fld) = item(fld)
                Else
                    itemdb(fld) = hfields(fld) 'default value
                End If
            Next
        End If
        Return True
    End Function

    'fore each name in $name - check if value is empty '' and make it null
    'not necessary in this framework As DB knows field types, it's here just for compatibility with php framework
    Public Shared Sub filterNullable(itemdb As Hashtable, name As String)
        Dim anames = Utils.qw(name)
        For Each fld As String In anames
            If itemdb.ContainsKey(fld) AndAlso itemdb(fld) = "" Then
                itemdb(fld) = Nothing
            End If
        Next
    End Sub


    'join ids from form to comma-separated string
    'sample:
    ' many <input name="dict_link_multi[<~id>]"...>
    ' itemdb("dict_link_multi") = FormUtils.multi2ids(reqh("dict_link_multi"))
    Public Shared Function multi2ids(items As Hashtable) As String
        If IsNothing(items) OrElse items.Count = 0 Then Return ""
        'Return String.Join(",", New ArrayList(items.Keys()).ToArray(GetType(String))) 'why not works properly in .NET 4???
        Return Join(New ArrayList(items.Keys()).ToArray(), ",")
    End Function

    'input: comma separated string
    'output: hashtable, keys=ids from input
    Public Shared Function ids2multi(str As String) As Hashtable
        Dim col As ArrayList = comma_str2col(str)
        Dim result As New Hashtable
        For Each id As String In col
            result(id) = 1
        Next
        Return result
    End Function

    Public Shared Function col2comma_str(col As ArrayList) As String
        Return Join(col.ToArray(), ",")
    End Function
    Public Shared Function comma_str2col(str As String) As ArrayList
        Dim result As ArrayList
        str = Trim(str)
        If str > "" Then
            result = New ArrayList(Split(str, ","))
        Else
            result = New ArrayList
        End If
        Return result
    End Function

    'return date for combo date selection or Nothing if wrong date
    'sample:
    ' <select name="item[fdate_combo_day]">
    ' <select name="item[fdate_combo_mon]">
    ' <select name="item[fdate_combo_year]">
    ' itemdb("fdate_combo") = FormUtils.date4combo(item, "fdate_combo")
    Public Shared Function dateForCombo(item As Hashtable, field_prefix As String) As Object
        Dim result As Object = Nothing
        Dim day As String = f2int(item(field_prefix & "_day"))
        Dim mon As String = f2int(item(field_prefix & "_mon"))
        Dim year As String = f2int(item(field_prefix & "_year"))

        If day > 0 AndAlso mon > 0 AndAlso year > 0 Then
            Try
                result = DateSerial(year, mon, day)
            Catch ex As Exception
                result = Nothing
            End Try
        End If

        Return result
    End Function

    Public Shared Function comboForDate(value As String, item As Hashtable, field_prefix As String) As Boolean
        Dim dt As DateTime
        If DateTime.TryParse(value, dt) Then
            item(field_prefix & "_day") = dt.Day()
            item(field_prefix & "_mon") = dt.Month()
            item(field_prefix & "_year") = dt.Year()
            Return True
        Else
            Return False
        End If

    End Function

    'input: 0-86400 (daily time in seconds)
    'output: HH:MM
    Public Shared Function intToTimeStr(i As Integer) As String
        Dim h As Integer = Math.Floor(i / 3600)
        Dim m As Integer = Math.Floor((i - h * 3600) / 60)
        Return h.ToString().PadLeft(2, "0") & ":" & m.ToString().PadLeft(2, "0")
    End Function

    'input: HH:MM
    'output: 0-86400 (daily time in seconds)
    Public Shared Function timeStrToInt(hhmm As String) As Integer
        Dim a() As String = Split(hhmm, ":", 2)
        Dim result As Integer = 0
        Try
            result = f2int(a(0)) * 3600 + f2int(a(1)) * 60
        Catch ex As Exception
            'can happen if input string in wrong format
        End Try
        Return result
    End Function

    Public Shared Function getIdFromAutocomplete(s As String) As Integer
        Dim result As Integer = 0
        Dim a() As String = Split(s, " - ", 2)
        Try
            result = Val(a(0))
        Catch ex As Exception
        End Try
        Return result
    End Function

    'convert time from field to 2 form fields with HH and MM suffixes
    'IN: hashtable to make changes in, field_name
    'OUT: false if item(field_name) wrong datetime
    Shared Function timeToForm(item As Hashtable, field_name As String) As Boolean
        Dim dt As DateTime
        If DateTime.TryParse(item(field_name), dt) Then
            item(field_name & "_hh") = dt.Hour()
            item(field_name & "_mm") = dt.Minute()
            item(field_name & "_ss") = dt.Second()
            Return True
        Else
            Return False
        End If
    End Function

    'opposite to time2from
    'OUT: false if can't create time from input item
    Shared Function formToTime(item As Hashtable, field_name As String) As Boolean
        Dim result As Boolean = True
        Dim hh As String = f2int(item(field_name & "_hh"))
        Dim mm As String = f2int(item(field_name & "_mm"))
        Dim ss As String = f2int(item(field_name & "_ss"))

        Try
            item(field_name) = TimeSerial(hh, mm, ss)
        Catch ex As Exception
            result = False
        End Try

        Return result
    End Function

    'datetime field to HH:MM or empty string (if no date set)
    Shared Function dateToFormTime(datestr As String) As String
        Dim result As String = ""
        If datestr > "" Then
            Dim dt = Utils.f2date(datestr)
            If dt IsNot Nothing Then
                result = DirectCast(dt, Date).ToString("HH:mm", System.Globalization.DateTimeFormatInfo.InvariantInfo)
            End If
        End If
        Return result
    End Function

    'date and time(HH:MM) fields to date object (or datestr if no time)
    'example: fields("dtfield") = FormUtils.formTimeToDate(itemdb("datefield"), reqh("item")("timefield"))
    Shared Function formTimeToDate(datestr As Object, timestr As String) As Object
        Dim result = datestr
        Dim timeint = FormUtils.timeStrToInt(timestr)
        Dim dt = Utils.f2date(datestr)
        If dt IsNot Nothing Then
            'if date set - add time
            result = dt.AddSeconds(timeint)
        End If
        Return result
    End Function

End Class
