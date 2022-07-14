' Miscellaneous framework utils
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Drawing.Drawing2D
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.Security.Cryptography
Imports System.Net

Public Class Utils
    Public Shared OLEDB_PROVIDER As String = "Microsoft.ACE.OLEDB.12.0" 'used for import from CSV/Excel, change it to your provider if necessary

    'convert "space" delimited string to an array
    'WARN! replaces all "&nbsp;" to spaces (after convert)
    Public Shared Function qw(ByVal str As String) As String()
        Dim arr() As String
        arr = Split(Trim(str), " ")

        For i As Integer = LBound(arr) To UBound(arr)
            If arr(i) Is Nothing Then arr(i) = ""
            arr(i) = Replace(arr(i), "&nbsp;", " ")
        Next

        Return arr
    End Function

    'convert from array (IList) back to qw-string
    'spaces converted to "&nbsp;"
    Public Shared Function qwRevert(ByVal slist As IList) As String
        Dim result As New StringBuilder()
        For Each el As String In slist
            result.Append(el.Replace(" ", "&nbsp;") & " ")
        Next
        Return result.ToString()
    End Function

    'convert string like "AAA|1 BBB|2 CCC|3 DDD" to hash
    'AAA => 1
    'BBB => 2
    'CCC => 3
    'DDD => 1 (default value 1)
    ' or "AAA BBB CCC DDD" => AAA=1, BBB=1, CCC=1, DDD=1
    'WARN! replaces all "&nbsp;" to spaces (after convert)
    Public Shared Function qh(str As String, Optional default_value As Object = 1) As Hashtable
        Dim result As New Hashtable
        If str IsNot Nothing AndAlso str > "" Then
            Dim arr() As String = Regex.Split(str, "\s+")
            For Each v As String In arr
                v = Replace(v, "&nbsp;", " ")
                Dim asub() As String = Split(v, "|", 2)
                Dim val As String = default_value
                If UBound(asub) > 0 Then val = asub(1)
                result.Add(asub(0), val)
            Next
        End If

        Return result
    End Function

    Public Shared Function qhRevert(ByVal sh As IDictionary) As String
        Dim result As New ArrayList()
        For Each key In sh.Keys
            result.Add(Replace(key.ToString(), " ", "&nbsp;") & "|" & sh(key))
        Next
        Return Join(result.ToArray(), " ")
    End Function


    'remove elements from hash, leave only those which keys passed
    Public Shared Sub hashFilter(hash As Hashtable, keys As String())
        Dim al_keys As New ArrayList(keys)
        Dim to_remove As New ArrayList
        For Each key As String In hash.Keys
            If al_keys.IndexOf(key) = -1 Then
                to_remove.Add(key)
            End If
        Next
        'remove keys
        For Each key As String In to_remove
            hash.Remove(key)
        Next
    End Sub

    'leave just allowed chars in string - for routers: controller, action or for route ID
    Public Shared Function routeFixChars(ByVal str As String) As String
        Return Regex.Replace(str, "[^A-Za-z0-9_-]+", "")
    End Function

    ''' <summary>
    ''' Split string exactly into 2 substrings using regular expression
    ''' </summary>
    ''' <param name="re">string suitable for RegEx</param>
    ''' <param name="source">string to be splitted</param>
    ''' <param name="dest1">ByRef destination string 1</param>
    ''' <param name="dest2">ByRef destination string 2</param>
    ''' <remarks></remarks>
    Public Shared Sub split2(re As String, source As String, ByRef dest1 As String, ByRef dest2 As String)
        dest1 = ""
        dest2 = ""
        Dim arr As String() = Regex.Split(source, re)
        If arr.Length > 0 Then dest1 = arr(0)
        If arr.Length > 1 Then dest2 = arr(1)
    End Sub

    'IN: email addresses delimited with ; space or newline
    'OUT: arraylist of email addresses
    Public Shared Function splitEmails(emails As String) As ArrayList
        Dim result As New ArrayList
        Dim arr As String() = Regex.Split(emails, "[; \n\r]+")
        For Each email As String In arr
            email = Trim(email)
            If email = "" Then Continue For
            result.Add(email)
        Next
        Return result
    End Function

    Public Shared Function htmlescape(ByVal str As String) As String
        str = HttpUtility.HtmlEncode(str)
        'str = Regex.Replace(str, "\&", "&amp;")
        'str = Regex.Replace(str, "\$", "&#36;")
        Return str
    End Function

    Public Shared Function str2url(ByVal str As String) As String
        If Not Regex.IsMatch(str, "^\w+://") Then
            str = "http://" & str
        End If
        Return str
    End Function

    Public Shared Function ConvertStreamToBase64(ByVal fs As Stream) As String
        Dim ReturnValue As String = ""

        Dim BinRead As IO.BinaryReader = New BinaryReader(fs)
        Dim BinBytes As Byte() = BinRead.ReadBytes(CInt(fs.Length))
        ReturnValue = Convert.ToBase64String(BinBytes)
        'Convert.ToBase64CharArray()

        Return ReturnValue
    End Function

    Public Shared Function f2bool(ByVal AField As Object) As Boolean
        Dim result As Boolean = False
        If AField Is Nothing Then Return False

        Boolean.TryParse(AField.ToString(), result)
        Return result
    End Function

    'TODO parse without Try/Catch
    Public Shared Function f2date(ByVal AField As String) As Object
        Dim result As Object = Nothing
        Try
            If IsNothing(AField) OrElse AField = "Null" OrElse AField = "" Then
                result = Nothing
            Else
                result = CDate(Trim(AField))
            End If
        Catch ex As Exception
            result = Nothing
        End Try

        Return result
    End Function

    'just return false if input cannot be converted to date
    Public Shared Function isDate(ByVal AField As Object) As Boolean
        Dim result As Object = f2date(AField)
        Return result IsNot Nothing
    End Function

    'guarantee to return string (if cannot convert to string - just return empty string)
    Public Shared Function f2str(ByVal AField As Object) As String
        If AField Is Nothing Then Return ""
        Dim result As String = Convert.ToString(AField)
        Return result
    End Function

    Public Shared Function f2int(ByVal AField As Object) As Integer
        If AField Is Nothing Then Return 0
        Dim result As Integer = 0

        Int32.TryParse(AField.ToString(), result)
        Return result
    End Function

    Public Shared Function f2decimal(ByVal AField As Object) As Decimal
        If AField Is Nothing Then Return 0
        Dim result As Decimal = 0

        Decimal.TryParse(AField.ToString(), result)
        Return result
    End Function

    'convert to double, optionally throw error
    Public Shared Function f2float(ByVal AField As Object, Optional is_error As Boolean = False) As Double
        Dim result As Double = 0

        If (AField Is Nothing OrElse Not Double.TryParse(AField.ToString(), result)) AndAlso is_error Then
            Throw New FormatException
        End If
        Return result
    End Function

    'just return false if input cannot be converted to float
    Public Shared Function isFloat(ByVal AField As Object) As Boolean
        Dim result As Double = 0
        Return Double.TryParse(AField, result)
    End Function

    Public Shared Function sTrim(ByVal str As String, ByVal size As Integer) As String
        If Len(str) > size Then str = Left(str, size) & "..."
        Return str
    End Function

    Public Shared Function getRandStr(ByVal size As Integer) As String
        Dim result As New StringBuilder
        Dim chars() As String = qw("A B C D E F a b c d e f 0 1 2 3 4 5 6 7 8 9")

        Randomize()
        For i As Integer = 1 To size
            result.Append(chars(CInt(Int((chars.Length - 1) * Rnd()))))
        Next

        Return result.ToString()
    End Function

    ''' <summary>
    ''' helper for importing csv files. Example:
    '''    Utils.importCSV(fw, AddressOf importer, "c:\import.csv")
    '''    Sub importer(row as Hashtable)
    '''       ...your custom import code
    '''    End Sub
    ''' </summary>
    ''' <param name="fw">fw instance</param>
    ''' <param name="callback">callback to custom code, accept one row of fields(as Hashtable)</param>
    ''' <param name="filepath">.csv file name to import</param>
    Public Shared Sub importCSV(fw As FW, callback As Action(Of Hashtable), filepath As String, Optional is_header As Boolean = True)
        Dim dir = Path.GetDirectoryName(filepath)
        Dim filename = Path.GetFileName(filepath)

        Dim ConnectionString As String = "Provider=" & OLEDB_PROVIDER & ";" +
                                "Data Source=" & dir & ";" &
                                "Extended Properties=""Text;HDR=" & IIf(is_header, "Yes", "No") & ";IMEX=1;FORMAT=Delimited"";"

        'Dim BOM As String = Chr(239) & Chr(187) & Chr(191) '"\uFEFF" '"ï»¿" '\xEF\xBB\xBF
        Using cn As New Data.OleDb.OleDbConnection(ConnectionString)
            cn.Open()

            Dim WorkSheetName = filename
            'quote as table name
            WorkSheetName = Replace(WorkSheetName, "[", "")
            WorkSheetName = Replace(WorkSheetName, "]", "")

            Dim sql = "select * from [" & WorkSheetName & "]"
            Dim dbcomm = New Data.OleDb.OleDbCommand(sql, cn)
            Dim dbread As Data.Common.DbDataReader = dbcomm.ExecuteReader()

            While dbread.Read()
                Dim row As New Hashtable
                For i = 0 To dbread.FieldCount - 1
                    'Dim value As String = dbread(i).ToString()
                    Dim value As Object = dbread(i)
                    Dim name As String = dbread.GetName(i).ToString()
                    'name = name.Replace(BOM, "")
                    row.Add(name, value)
                Next

                'logger(h)
                callback(row)
            End While
        End Using
    End Sub

    ''' <summary>
    ''' helper for importing Excel files. Example:
    '''    Utils.importExcel(fw, AddressOf importer, "c:\import.xlsx")
    '''    Sub importer(sheet_name as String, rows as ArrayList)
    '''       ...your custom import code
    '''    End Sub
    ''' </summary>
    ''' <param name="fw">fw instance</param>
    ''' <param name="callback">callback to custom code, accept worksheet name and all rows(as ArrayList of Hashtables)</param>
    ''' <param name="filepath">.xlsx file name to import</param>
    ''' <param name="is_header"></param>
    ''' <returns></returns>
    Public Shared Function importExcel(fw As FW, callback As Action(Of String, ArrayList), filepath As String, Optional is_header As Boolean = True) As Hashtable
        Dim result As New Hashtable()
        Dim conf As New Hashtable From {
                {"type", "OLE"},
                {"connection_string", "Provider=" & OLEDB_PROVIDER & ";Data Source=" & filepath & ";Extended Properties=""Excel 12.0 Xml;HDR=" & IIf(is_header, "Yes", "No") & ";ReadOnly=True;IMEX=1"""}
            }
        Dim accdb = New DB(fw, conf)
        Dim conn As System.Data.OleDb.OleDbConnection = accdb.connect()
        Dim schema = conn.GetOleDbSchemaTable(Data.OleDb.OleDbSchemaGuid.Tables, Nothing)
        If schema Is Nothing OrElse schema.Rows.Count < 1 Then
            Throw New ApplicationException("No worksheets found in the Excel file")
        End If

        Dim where As New Hashtable
        For i As Integer = 0 To schema.Rows.Count - 1
            Dim sheet_name_full = schema.Rows(i)("TABLE_NAME").ToString()
            Dim sheet_name = sheet_name_full.Replace("""", "")
            sheet_name = sheet_name.Replace("'", "")
            sheet_name = sheet_name.Substring(0, sheet_name.Length - 1)
            Try
                Dim rows = accdb.array(sheet_name_full, where)
                callback(sheet_name, rows)
            Catch ex As Exception
                Throw New ApplicationException("Error while reading data from [" & sheet_name & "] sheet: " & ex.Message())
            End Try
        Next
        ' close connection to release the file
        accdb.disconnect()

        Return result
    End Function


    Public Shared Function toCSVRow(row As Hashtable, fields As Array) As String
        Dim result As New StringBuilder
        Dim is_first = True
        For Each fld As String In fields
            If Not is_first Then result.Append(",")

            Dim str As String = Regex.Replace(row(fld) & "", "[\n\r]+", " ")
            str = Replace(str, """", """""")
            'check if string need to be quoted (if it contains " or ,)
            If InStr(str, """") > 0 OrElse InStr(str, ",") > 0 Then
                str = """" & str & """"
            End If
            result.Append(str)
            is_first = False
        Next
        Return result.ToString()
    End Function

    ''' <summary>
    ''' standard function for exporting to csv
    ''' </summary>
    ''' <param name="csv_export_headers">CSV headers row, comma-separated format</param>
    ''' <param name="csv_export_fields">empty, * or Utils.qw format</param>
    ''' <param name="rows">DB array</param>
    ''' <returns></returns>
    Public Shared Function getCSVExport(csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As StringBuilder
        Dim headers_str As String = csv_export_headers
        Dim csv As New StringBuilder
        Dim fields() As String = Nothing
        If csv_export_fields = "" Or csv_export_fields = "*" Then
            'just read field names from first row
            If rows.Count > "" Then
                fields = New ArrayList(DirectCast(rows(0).Keys(), ICollection)).ToArray()
                headers_str = Join(fields, ",")
            End If
        Else
            fields = Utils.qw(csv_export_fields)
        End If

        csv.Append(headers_str & vbLf)
        For Each row As Hashtable In rows
            csv.Append(Utils.toCSVRow(row, fields) & vbLf)
        Next
        Return csv
    End Function

    Public Shared Function writeCSVExport(response As HttpResponse, filename As String, csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As Boolean
        filename = Replace(filename, """", "'") 'quote doublequotes

        response.AppendHeader("Content-type", "text/csv")
        response.AppendHeader("Content-Disposition", "attachment; filename=""" & filename & """")

        response.Write(Utils.getCSVExport(csv_export_headers, csv_export_fields, rows))
        Return True
    End Function

    Public Shared Function writeXLSExport(fw As FW, filename As String, csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As Boolean
        Dim ps As New Hashtable
        ps("rows") = rows

        Dim headers As New ArrayList
        For Each str As String In csv_export_headers.Split(",")
            Dim h As New Hashtable
            h("iname") = str
            headers.Add(h)
        Next
        ps("headers") = headers

        Dim fields() As String = Utils.qw(csv_export_fields)
        For Each row As Hashtable In rows
            Dim cell As New ArrayList
            For Each f As String In fields
                Dim h As New Hashtable
                h("value") = row(f)
                cell.Add(h)
            Next
            row("cell") = cell
        Next

        'parse and out document
        'TODO ConvUtils.parse_page_xls(fw, LCase(fw.cur_controller_path & "/index/export"), "xls.html", hf, "filename")

        Dim parser As ParsePage = New ParsePage(fw)
        'Dim tpl_dir = LCase(fw.cur_controller_path & "/index/export")
        Dim tpl_dir = "/common/list/export"
        Dim page As String = parser.parse_page(tpl_dir, "xls.html", ps)

        filename = filename.Replace("""", "_")

        fw.resp.AddHeader("Content-type", "application/vnd.ms-excel")
        fw.resp.AddHeader("Content-Disposition", "attachment; filename=""" & filename & """")
        fw.resp.Write(page)
    End Function

    'Detect orientation and auto-rotate correctly
    Public Shared Function rotateImage(Image As Image) As Boolean
        Dim result = False
        Dim rot = RotateFlipType.RotateNoneFlipNone
        Dim props = Image.PropertyItems()

        For Each p In props
            If p.Id = 274 Then
                Select Case BitConverter.ToInt16(p.Value, 0)
                    Case 1
                        rot = RotateFlipType.RotateNoneFlipNone
                    Case 3
                        rot = RotateFlipType.Rotate180FlipNone
                    Case 6
                        rot = RotateFlipType.Rotate90FlipNone
                    Case 8
                        rot = RotateFlipType.Rotate270FlipNone
                End Select
            End If
        Next
        If rot <> RotateFlipType.RotateNoneFlipNone Then
            Image.RotateFlip(rot)
            result = True
        End If
        Return result
    End Function

    'resize image in from_file to w/h and save to to_file
    '(optional)w and h - mean max weight and max height (i.e. image will not be upsized if it's smaller than max w/h)
    'if no w/h passed - then no resizing occurs, just conversion (based on destination extension)
    'return false if no resize performed (if image already smaller than necessary). Note if to_file is not same as from_file - to_file will have a copy of the from_file
    Public Shared Function resizeImage(ByVal from_file As String, ByVal to_file As String, Optional ByVal w As Long = -1, Optional ByVal h As Long = -1) As Boolean
        Dim stream As New FileStream(from_file, FileMode.Open, FileAccess.Read)

        ' Create new image.
        Dim image As System.Drawing.Image = System.Drawing.Image.FromStream(stream)

        'Detect orientation and auto-rotate correctly
        Dim is_rotated = rotateImage(image)

        ' Calculate proportional max width and height.
        Dim oldWidth As Integer = image.Width
        Dim oldHeight As Integer = image.Height

        If w = -1 Then w = oldWidth
        If h = -1 Then h = oldHeight

        If oldWidth / w >= 1 Or oldHeight / h >= 1 Then
            'downsizing
        Else
            'image already smaller no resize required - keep sizes same
            image.Dispose()
            stream.Close()
            If to_file <> from_file Then
                'but if destination file is different - make a copy
                File.Copy(from_file, to_file)
            End If
            Return False
        End If

        If (CDec(oldWidth) / CDec(oldHeight)) > (CDec(w) / CDec(h)) Then
            Dim ratio As Decimal = CDec(w) / oldWidth
            h = CInt(oldHeight * ratio)
        Else
            Dim ratio As Decimal = CDec(h) / oldHeight
            w = CInt(oldWidth * ratio)
        End If

        ' Create a new bitmap with the same resolution as the original image.
        Dim bitmap As New Bitmap(w, h, PixelFormat.Format24bppRgb)
        bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution)

        ' Create a new graphic.
        Dim gr As Graphics = Graphics.FromImage(bitmap)
        gr.Clear(Color.White)
        gr.InterpolationMode = InterpolationMode.HighQualityBicubic
        gr.SmoothingMode = SmoothingMode.HighQuality
        gr.PixelOffsetMode = PixelOffsetMode.HighQuality
        gr.CompositingQuality = CompositingQuality.HighQuality

        ' Create a scaled image based on the original.
        gr.DrawImage(image, New Rectangle(0, 0, w, h), New Rectangle(0, 0, oldWidth, oldHeight), GraphicsUnit.Pixel)
        gr.Dispose()

        ' Save the scaled image.
        Dim ext As String = UploadUtils.getUploadFileExt(to_file)
        Dim out_format As ImageFormat = image.RawFormat
        Dim EncoderParameters As EncoderParameters = Nothing
        Dim ImageCodecInfo As ImageCodecInfo = Nothing

        If ext = ".gif" Then
            out_format = ImageFormat.Gif
        ElseIf ext = ".jpg" Then
            out_format = ImageFormat.Jpeg
            'set jpeg quality to 80
            ImageCodecInfo = GetEncoderInfo(out_format)
            Dim Encoder As Encoder = Encoder.Quality
            EncoderParameters = New EncoderParameters(1)
            EncoderParameters.Param(0) = New EncoderParameter(Encoder, CType(80L, Int32))
        ElseIf ext = ".png" Then
            out_format = ImageFormat.Png
        End If

        'close read stream before writing as to_file might be same as from_file
        image.Dispose()
        stream.Close()

        If EncoderParameters Is Nothing Then
            bitmap.Save(to_file, out_format) 'image.RawFormat
        Else
            bitmap.Save(to_file, ImageCodecInfo, EncoderParameters)
        End If
        bitmap.Dispose()

        'if( contentType == "image/gif" )
        '{
        '            Using (thumbnail)
        '    {
        '        OctreeQuantizer quantizer = new OctreeQuantizer ( 255 , 8 ) ;
        '        using ( Bitmap quantized = quantizer.Quantize ( bitmap ) )
        '        {
        '            Response.ContentType = "image/gif";
        '            quantized.Save ( Response.OutputStream , ImageFormat.Gif ) ;
        '        }
        '    }
        '}

        Return True
    End Function

    Private Shared Function GetEncoderInfo(ByVal format As ImageFormat) As ImageCodecInfo
        Dim j As Integer
        Dim encoders() As ImageCodecInfo
        encoders = ImageCodecInfo.GetImageEncoders()

        j = 0
        While j < encoders.Length
            If encoders(j).FormatID = format.Guid Then
                Return encoders(j)
            End If
            j += 1
        End While
        Return Nothing

    End Function 'GetEncoderInfo

    Public Shared Function fileSize(filepath As String) As Long
        Dim fi As FileInfo = New FileInfo(filepath)
        Return fi.Length
    End Function

    'extract just file name (with ext) from file path
    Public Shared Function fileName(filepath As String) As String
        Return System.IO.Path.GetFileName(filepath)
    End Function

    ''' <summary>
    ''' Merge hashes - copy all key-values from hash2 to hash1 with overwriting existing keys
    ''' </summary>
    ''' <param name="hash1"></param>
    ''' <param name="hash2"></param>
    ''' <remarks></remarks>
    Public Shared Sub mergeHash(ByRef hash1 As Hashtable, ByRef hash2 As Hashtable)
        If hash2 IsNot Nothing Then
            Dim keys As New ArrayList(hash2.Keys) 'make static copy of hash2.keys, so even if hash2.keys changing (ex: hash1 is same as hash2) it will not affect the loop
            For Each key As String In keys
                hash1(key) = hash2(key)
            Next
        End If
    End Sub

    'deep hash merge, i.e. if hash2 contains values that is hash value - go in it and copy such values to hash2 at same place accordingly
    'recursive
    Public Shared Sub mergeHashDeep(ByRef hash1 As Hashtable, ByRef hash2 As Hashtable)
        If hash2 IsNot Nothing Then
            Dim keys As New ArrayList(hash2.Keys)
            For Each key As String In keys
                If TypeOf hash2(key) Is Hashtable Then
                    If Not (TypeOf hash1(key) Is Hashtable) Then
                        hash1(key) = New Hashtable
                    End If
                    mergeHashDeep(hash1(key), hash2(key))
                Else
                    hash1(key) = hash2(key)
                End If
            Next
        End If
    End Sub

    Public Shared Function bytes2str(b As Integer) As String
        Dim result As String = b

        If b < 1024 Then
            result &= " B"
        ElseIf b < 1048576 Then
            result = (Math.Floor(b / 1024 * 100) / 100) & " KiB"
        ElseIf b < 1073741824 Then
            result = (Math.Floor(b / 1048576 * 100) / 100) & " MiB"
        Else
            result = (Math.Floor(b / 1073741824 * 100) / 100) & " GiB"
        End If
        Return result
    End Function

    ''' <summary>
    ''' convert data structure to JSON string
    ''' </summary>
    ''' <param name="data">any data like single value, arraylist, hashtable, etc..</param>
    ''' <returns></returns>
    Public Shared Function jsonEncode(data As Object) As String
        Dim des = New Script.Serialization.JavaScriptSerializer()
        des.MaxJsonLength = Integer.MaxValue
        Return des.Serialize(data)
    End Function

    ''' <summary>
    ''' convert JSON string into data structure
    ''' </summary>
    ''' <param name="str">JSON string</param>
    ''' <returns>single value, arraylist, hashtable, etc.. or Nothing if cannot be converted</returns>
    ''' <remarks>Note, JavaScriptSerializer.MaxJsonLength is about 4MB unicode</remarks>
    Public Shared Function jsonDecode(str As String) As Object
        Dim result As Object
        Try
            Dim des = New Script.Serialization.JavaScriptSerializer()
            des.MaxJsonLength = Integer.MaxValue
            result = des.DeserializeObject(str)
            result = cast2std(result)

        Catch ex As Exception
            'if error during conversion - return Nothing
            FW.Current.logger(ex.Message)
            result = Nothing
        End Try

        Return result
    End Function

    ''' <summary>
    ''' depp convert data structure to standard framework's Hashtable/Arraylist
    ''' </summary>
    ''' <param name="data"></param>
    ''' <remarks>RECURSIVE!</remarks>
    Public Shared Function cast2std(data As Object) As Object
        Dim result As Object = data

        If TypeOf result Is IDictionary Then
            'convert dictionary to Hashtable
            Dim result2 = New Hashtable 'because we can't iterate hashtable and change it
            For Each key In CType(result, IDictionary).Keys
                result2(key) = cast2std(result(key))
            Next
            result = result2

        ElseIf TypeOf result Is IList Then
            'convert arrays to ArrayList
            result = New ArrayList(CType(result, IList))
            For i = 0 To result.Count - 1
                result(i) = cast2std(result(i))
            Next
        End If

        Return result
    End Function

    'serialize using BinaryFormatter.Serialize
    'return as base64 string
    Public Shared Function serialize(data As Object) As String
        Dim xstream As New IO.MemoryStream
        Dim xformatter As New BinaryFormatter

        xformatter.Serialize(xstream, data)

        Return Convert.ToBase64String(xstream.ToArray())
    End Function

    'deserialize base64 string serialized with Utils.serialize
    'return object or Nothing (if error)
    Public Shared Function deserialize(ByRef str As String) As Object
        Dim data As Object
        Try
            Dim xstream As MemoryStream
            xstream = New MemoryStream(Convert.FromBase64String(str))
            Dim xformatter As New BinaryFormatter
            data = xformatter.Deserialize(xstream)
        Catch ex As Exception
            data = Nothing
        End Try

        Return data
    End Function

    'return Hashtable keys as an array
    Public Shared Function hashKeys(h As Hashtable) As String()
        Return New ArrayList(h.Keys).ToArray(GetType(String))
    End Function

    'capitalize first word in string
    'if mode='all' - capitalize all words
    'EXAMPLE: mode="" : sample string => Sample string
    'mode="all" : sample STRING => Sample String
    Shared Function capitalize(str As String, Optional mode As String = "") As Object
        If mode = "all" Then
            str = LCase(str)
            str = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str)
        Else
            str = str.Substring(0, 1).ToUpper() & str.Substring(1)
        End If

        Return str
    End Function

    'repeat string num times
    Shared Function strRepeat(str As String, num As Integer) As String
        Dim result As New StringBuilder
        For i As Integer = 1 To num
            result.Append(str)
        Next
        Return result.ToString
    End Function

    'return unique file name in form UUID (without extension)
    Public Shared Function uuid() As String
        Return System.Guid.NewGuid().ToString()
    End Function

    'return path to tmp filename WITHOUT extension
    Public Shared Function getTmpFilename(Optional prefix As String = "osafw") As String
        Return Path.GetTempPath & "\" & prefix & Utils.uuid()
    End Function

    'scan tmp directory, find all tmp files created by website and delete older than 1 hour
    Public Shared Sub cleanupTmpFiles(Optional prefix As String = "osafw")
        Dim files As String() = Directory.GetFiles(Path.GetTempPath(), prefix & "*")
        For Each file As String In files
            Dim fi As FileInfo = New FileInfo(file)
            If DateDiff(DateInterval.Minute, fi.CreationTime, Now()) > 60 Then
                fi.Delete()
            End If
        Next
    End Sub

    'return md5 hash (hexadecimals) for a string
    Public Shared Function md5(str As String) As String
        'convert string to bytes
        Dim ustr As New UTF8Encoding
        Dim bstr() As Byte = ustr.GetBytes(str)

        Dim md5hasher As MD5 = MD5CryptoServiceProvider.Create()
        Dim bhash() As Byte = md5hasher.ComputeHash(bstr)

        'convert hash value to hex string
        Dim sb As New System.Text.StringBuilder
        For Each one_byte As Byte In bhash
            sb.Append(one_byte.ToString("x2").ToUpper)
        Next

        Return sb.ToString().ToLower()
    End Function

    '1 => 01
    '10 => 10
    Shared Function toXX(str As String) As String
        If Len(str) < 2 Then str = "0" & str
        Return str
    End Function

    Shared Function num2ordinal(num As Integer) As String
        If num <= 0 Then Return num.ToString()

        Select Case num Mod 100
            Case 11
            Case 12
            Case 13
                Return num & "th"
        End Select

        Select Case num Mod 10
            Case 1
                Return num & "st"
            Case 2
                Return num & "nd"
            Case 3
                Return num & "rd"
            Case Else
                Return num & "th"
        End Select
    End Function

    ' truncate  - This truncates a variable to a character length, the default is 80.
    ' trchar    - As an optional second parameter, you can specify a string of text to display at the end if the variable was truncated.
    ' The characters in the string are included with the original truncation length.
    ' trword    - 0/1. By default, truncate will attempt to cut off at a word boundary =1.
    ' trend     - 0/1. If you want to cut off at the exact character length, pass the optional third parameter of 1.
    '<~tag truncate="80" trchar="..." trword="1" trend="1">
    Shared Function str2truncate(str As String, hattrs As Hashtable) As Object
        Dim trlen As Integer = 80
        Dim trchar As String = "..."
        Dim trword As Integer = 1
        Dim trend As Integer = 1  'if trend=0 trword - ignored

        If hattrs("truncate") > "" Then
            Dim trlen1 As Integer = f2int(hattrs("truncate"))
            If trlen1 > 0 Then trlen = trlen1
        End If
        If hattrs.ContainsKey("trchar") Then trchar = hattrs("trchar")
        If hattrs.ContainsKey("trend") Then trend = hattrs("trend")
        If hattrs.ContainsKey("trword") Then trword = hattrs("trword")

        Dim orig_len As Integer = Len(str)
        If orig_len < trlen Then Return str 'no need truncate

        If trend = 1 Then
            If trword = 1 Then
                str = Regex.Replace(str, "^(.{" & trlen & ",}?)[\n \t\.\,\!\?]+(.*)$", "$1", RegexOptions.Singleline)
                If Len(str) < orig_len Then str &= trchar
            Else
                str = Left(str, trlen) & trchar
            End If
        Else
            str = Left(str, trlen / 2) & trchar & Mid(str, trlen / 2 + 1)
        End If
        Return str
    End Function

    'IN: orderby string for default asc sorting, ex: "id", "id desc", "prio desc, id"
    'OUT: orderby or inversed orderby (if sortdir="desc"), ex: "id desc", "id asc", "prio asc, id desc"
    Shared Function orderbyApplySortdir(orderby As String, sortdir As String) As String
        Dim result As String = orderby

        If sortdir = "desc" Then
            'TODO - move this to fw utils
            Dim order_fields As New ArrayList
            For Each fld As String In orderby.Split(",")
                'if fld contains asc or desc - change to opposite
                If InStr(fld, " asc") Then
                    fld = Replace(fld, " asc", " desc")
                ElseIf InStr(fld, "desc") Then
                    fld = Replace(fld, " desc", " asc")
                Else
                    'if no asc/desc - just add desc at the end
                    fld &= " desc"
                End If
                order_fields.Add(fld)
            Next
            'result = String.Join(", ", order_fields.ToArray(GetType(String))) 'net 2
            result = Join(New ArrayList(order_fields).ToArray(), ", ") 'net 4
        End If

        Return result
    End Function

    Shared Function html2text(str As String) As String
        str = Regex.Replace(str, "\n+", " ")
        str = Regex.Replace(str, "<br\s*\/?>", vbLf)
        str = Regex.Replace(str, "(?:<[^>]*>)+", " ")
        Return str
    End Function

    'sel_ids - comma-separated ids
    'value:
    '     nothing - use id value from input
    '     "123..."  - use index (by order)
    '     "other value" - use this value
    'return hash: id => id
    Shared Function commastr2hash(sel_ids As String, Optional value As String = Nothing) As Hashtable
        Dim ids As New ArrayList(Split(sel_ids, ","))
        Dim result As New Hashtable
        For i = 0 To ids.Count - 1
            Dim v As String = ids(i)
            result(v) = IIf(IsNothing(value), v, IIf(value = "123...", i, value))
        Next
        Return result
    End Function

    'comma-delimited str to newline-delimited str
    Public Shared Function commastr2nlstr(str As String) As String
        Return Replace(str, ",", vbCrLf)
    End Function

    'newline-delimited str to comma-delimited str
    Public Shared Function nlstr2commastr(str As String) As String
        Return Regex.Replace(str & "", "[\n\r]+", ",")
    End Function

    ''' <summary>
    ''' for each row in rows add keys/values to this row (by ref)
    ''' </summary>
    ''' <param name="rows">db array</param>
    ''' <param name="fields">keys/values to add</param>
    Public Shared Sub arrayInject(rows As ArrayList, fields As Hashtable)
        For Each row As Hashtable In rows
            'array merge
            For Each key In fields.Keys
                row(key) = fields(key)
            Next
        Next
    End Sub

    ''' <summary>
    ''' escapes/encodes string so it can be passed as part of the url
    ''' </summary>
    ''' <param name="str"></param>
    ''' <returns></returns>
    Public Shared Function urlescape(str As String) As String
        Return HttpUtility.UrlEncode(str)
    End Function

    'sent multipart/form-data POST request to remote URL with files (key=fieldname, value=filepath) and formFields
    Shared Function UploadFilesToRemoteUrl(ByVal url As String, ByVal files As Hashtable, ByVal Optional formFields As NameValueCollection = Nothing, Optional cert As X509Certificates.X509Certificate2 = Nothing) As String
        Dim boundary As String = "----------------------------" & DateTime.Now.Ticks.ToString("x")
        Dim request As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
        request.ContentType = "multipart/form-data; boundary=" & boundary
        request.Method = "POST"
        request.KeepAlive = True
        If cert IsNot Nothing Then request.ClientCertificates.Add(cert)

        Dim memStream As New System.IO.MemoryStream()
        Dim boundarybytes = System.Text.Encoding.ASCII.GetBytes(vbCrLf & "--" & boundary & vbCrLf)
        Dim endBoundaryBytes = System.Text.Encoding.ASCII.GetBytes(vbCrLf & "--" & boundary & "--")

        'Dim formdataTemplate As String = vbCrLf & "--" & boundary & vbCrLf & "Content-Disposition: form-data; name=""{0}"";" & vbCrLf & vbCrLf & "{1}"
        Dim formdataTemplate As String = "--" & boundary & vbCrLf & "Content-Disposition: form-data; name=""{0}"";" & vbCrLf & vbCrLf & "{1}" & vbCrLf
        If formFields IsNot Nothing Then
            For Each key As String In formFields.Keys
                Dim formitem As String = String.Format(formdataTemplate, key, formFields(key))

                If memStream.Length > 0 Then formitem = vbCrLf & formitem 'add crlf before the string only for second and further lines

                Dim formitembytes As Byte() = System.Text.Encoding.UTF8.GetBytes(formitem)
                memStream.Write(formitembytes, 0, formitembytes.Length)
            Next
        End If

        Dim headerTemplate As String = "Content-Disposition: form-data; name=""{0}""; filename=""{1}""" & vbCrLf & "Content-Type: {2}" & vbCrLf & vbCrLf
        For Each fileField As String In files.Keys
            memStream.Write(boundarybytes, 0, boundarybytes.Length)

            'mime (TODO use System.Web.MimeMapping.GetMimeMapping() for .net 4.5+)
            Dim mimeType = "application/octet-stream"
            If System.IO.Path.GetExtension(files(fileField)) = ".xml" Then mimeType = "text/xml"

            Dim header = String.Format(headerTemplate, fileField, System.IO.Path.GetFileName(files(fileField)), mimeType)
            Dim headerbytes = System.Text.Encoding.UTF8.GetBytes(header)
            memStream.Write(headerbytes, 0, headerbytes.Length)

            Using fileStream = New FileStream(files(fileField), FileMode.Open, FileAccess.Read)
                Dim buffer = New Byte(1023) {}
                Dim bytesRead = fileStream.Read(buffer, 0, buffer.Length)
                While bytesRead <> 0
                    memStream.Write(buffer, 0, bytesRead)
                    bytesRead = fileStream.Read(buffer, 0, buffer.Length)
                End While
            End Using
        Next

        memStream.Write(endBoundaryBytes, 0, endBoundaryBytes.Length)
        'Diagnostics.Debug.WriteLine("***")
        'Diagnostics.Debug.WriteLine(Encoding.ASCII.GetString(memStream.ToArray()))
        'Diagnostics.Debug.WriteLine("***")

        request.ContentLength = memStream.Length
        Using requestStream = request.GetRequestStream()
            memStream.Position = 0

            Dim tempBuffer As Byte() = New Byte(memStream.Length - 1) {}
            memStream.Read(tempBuffer, 0, tempBuffer.Length)
            memStream.Close()
            requestStream.Write(tempBuffer, 0, tempBuffer.Length)
        End Using

        Using response = request.GetResponse()
            Dim stream2 = response.GetResponseStream()
            Dim reader2 As New StreamReader(stream2)
            Return reader2.ReadToEnd()
        End Using

    End Function

    'convert/normalize external table/field name to fw standard name
    '"SomeCrazy/Name" => "some_crazy_name"
    Shared Function name2fw(str As String) As String
        Dim result = str
        result = Regex.Replace(result, "^tbl|dbo", "", RegexOptions.IgnoreCase) 'remove tbl,dbo prefixes if any
        result = Regex.Replace(result, "([A-Z]+)", "_$1") 'split CamelCase to underscore, but keep abbrs together ZIP/Code -> zip_code

        result = Regex.Replace(result, "\W+", "_") 'replace all non-alphanum to underscore
        result = Regex.Replace(result, "_+", "_") 'deduplicate underscore
        result = Regex.Replace(result, "^_+|_+$", "") 'remove first and last _ if any
        result = result.ToLower() 'and finally to lowercase
        result = result.Trim()
        Return result
    End Function

    'convert some system name to human-friendly name'
    '"system_name_id" => "System Name ID"
    Shared Function name2human(str As String) As String
        'first - check predefined
        Dim str_lc = str.ToLower()
        If str_lc = "icode" Then Return "Code"
        If str_lc = "iname" Then Return "Name"
        If str_lc = "idesc" Then Return "Description"
        If str_lc = "id" Then Return "ID"
        If str_lc = "fname" Then Return "First Name"
        If str_lc = "lname" Then Return "Last Name"
        If str_lc = "midname" Then Return "Middle Name"

        Dim result = str
        result = Regex.Replace(result, "^tbl|dbo", "", RegexOptions.IgnoreCase) 'remove tbl prefix if any
        result = Regex.Replace(result, "_+", " ") 'underscores to spaces
        result = Regex.Replace(result, "([a-z ])([A-Z]+)", "$1 $2") 'split CamelCase words
        result = Regex.Replace(result, " +", " ") 'deduplicate spaces
        result = Utils.capitalize(result, "all") 'Title Case

        If Regex.IsMatch(result, "\bid\b", RegexOptions.IgnoreCase) Then
            'if contains id/ID - remove it and make singular
            result = Regex.Replace(result, "\bid\b", "", RegexOptions.IgnoreCase)
            result = Regex.Replace(result, "(?:es|s)\s*$", "", RegexOptions.IgnoreCase) 'remove -es or -s at the end
        End If

        result = result.Trim()
        Return result
    End Function

    'convert c/snake style name to CamelCase
    'system_name => SystemName
    Shared Function nameCamelCase(str As String) As String
        Dim result = str
        result = Regex.Replace(result, "\W+", " ") 'non-alphanum chars to spaces
        result = Utils.capitalize(result)
        result = Regex.Replace(result, " +", "") 'remove spaces
        Return str
    End Function

End Class
