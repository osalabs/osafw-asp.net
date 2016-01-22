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

Public Class Utils
    'convert "space" delimited string to an array
    'WARN! replaces all "&nbsp;" to spaces (after convert)
    Public Shared Function qw(ByVal str As String) As String()
        Dim arr() As String
        arr = Split(Trim(str), " ")

        For i As Integer = LBound(arr) To UBound(arr)
            arr(i) = Replace(arr(i), "&nbsp;", " ")
        Next

        Return arr
    End Function

    'convert string like "AAA|1 BBB|2 CCC|3 DDD" to hash
    'AAA => 1
    'BBB => 2
    'CCC => 3
    'DDD => 1 (default value 1)
    ' or "AAA BBB CCC DDD" => AAA=1, BBB=1, CCC=1, DDD=1
    'WARN! replaces all "&nbsp;" to spaces (after convert)
    Public Shared Function qh(str As String, Optional default_value As Object = 1) As Hashtable
        Dim result As Hashtable = New Hashtable
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

    'remove elements from hash, leave only those which keys passed
    Public Shared Sub hashfilter(hash As Hashtable, keys As String())
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
    Public Shared Function route_fix_chars(ByVal str As String) As String
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
    Public Shared Function email_split(emails As String) As ArrayList
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

    Public Shared Function f2int(ByVal AField As Object) As Integer
        Dim result As Integer = 0
        If AField Is Nothing Then Return 0

        Int32.TryParse(AField.ToString(), result)
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
    Public Shared Function is_float(ByVal AField As Object) As Boolean
        Dim result As Double = 0
        Return Double.TryParse(AField, result)
    End Function

    Public Shared Function strim(ByVal str As String, ByVal size As Integer) As String
        If Len(str) > size Then str = Left(str, size) & "..."
        Return str
    End Function

    Public Shared Function get_rand_str(ByVal size As Integer) As String
        Dim result As New StringBuilder
        Dim chars() As String = qw("A B C D E F a b c d e f 0 1 2 3 4 5 6 7 8 9")

        Randomize()
        For i As Integer = 1 To size
            result.Append(chars(CInt(Int((chars.Length - 1) * Rnd()))))
        Next

        Return result.ToString()
    End Function

    Public Shared Function to_csv_row(row As Hashtable, fields As Array) As String
        Dim result As New StringBuilder
        For Each fld As String In fields
            If result.Length > 0 Then result.Append(",")

            Dim str As String = Regex.Replace(row(fld) & "", "[\n\r]+", " ")
            str = Replace(str, """", """""")
            'check if string need to be quoted (if it contains " or ,)
            If InStr(str, """") > 0 OrElse InStr(str, ",") > 0 Then
                str = """" & str & """"
            End If
            result.Append(str)
        Next
        Return result.ToString()
    End Function

    'standard function for exporting to csv
    Public Shared Function get_csv_export(csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As StringBuilder
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
            fields = Split(csv_export_fields, ",")
        End If

        csv.Append(headers_str & vbLf)
        For Each row As Hashtable In rows
            csv.Append(Utils.to_csv_row(row, fields) & vbLf)
        Next
        Return csv
    End Function

    Public Shared Function write_csv_export(response As HttpResponse, filename As String, csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As Boolean
        filename = Replace(filename, """", "'") 'quote doublequotes

        response.AppendHeader("Content-type", "text/csv")
        response.AppendHeader("Content-Disposition", "attachment; filename=""" & filename & """")

        response.Write(Utils.get_csv_export(csv_export_headers, csv_export_fields, rows))
        Return True
    End Function


    'resize image in from_file to w/h and save to to_file
    'w and h - mean max weight and max height (i.e. image will not be upsized if it's smaller than max w/h)
    'return false if no resize performed (if image already smaller than necessary). Note if to_file is not same as from_file - to_file will have a copy of the from_file
    Public Shared Function image_resize(ByVal from_file As String, ByVal to_file As String, ByVal w As Long, ByVal h As Long) As Boolean
        Dim stream As New FileStream(from_file, FileMode.Open, FileAccess.Read)

        ' Create new image.
        Dim image As System.Drawing.Image = System.Drawing.Image.FromStream(stream)

        ' Calculate proportional max width and height.
        Dim oldWidth As Integer = image.Width
        Dim oldHeight As Integer = image.Height

        If oldWidth / w > 1 Or oldHeight / h > 1 Then
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
        Dim ext As String = UploadUtils.get_upload_file_ext(to_file)
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

    Public Shared Function file_size(filepath As String) As Long
        Dim fi As FileInfo = New FileInfo(filepath)
        Return fi.Length
    End Function

    'extract just file name (with ext) from file path
    Public Shared Function file_name(filepath As String) As String
        Return System.IO.Path.GetFileName(filepath)
    End Function

    ''' <summary>
    ''' Merge hashes - copy all key-values from hash2 to hash1 with overwriting existing keys
    ''' </summary>
    ''' <param name="hash1"></param>
    ''' <param name="hash2"></param>
    ''' <remarks></remarks>
    Public Shared Sub hash_merge(ByRef hash1 As Hashtable, ByRef hash2 As Hashtable)
        If hash2 IsNot Nothing Then
            Dim keys As New ArrayList(hash2.Keys) 'make static copy of hash2.keys, so even if hash2.keys changing (ex: hash1 is same as hash2) it will not affect the loop
            For Each key As String In keys
                hash1(key) = hash2(key)
            Next
        End If
    End Sub

    'deep hash merge, i.e. if hash2 contains values that is hash value - go in it and copy such values to hash2 at same place accordingly
    'recursive
    Public Shared Sub hash_merge_deep(ByRef hash1 As Hashtable, ByRef hash2 As Hashtable)
        If hash2 IsNot Nothing Then
            Dim keys As New ArrayList(hash2.Keys)
            For Each key As String In keys
                If TypeOf hash2(key) Is Hashtable Then
                    If Not (TypeOf hash1(key) Is Hashtable) Then
                        hash1(key) = New Hashtable
                    End If
                    hash_merge_deep(hash1(key), hash2(key))
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

    'TODO maybe use JsonConvert.SerializeObject?
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
    Public Shared Function hash_keys(h As Hashtable) As String()
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
    Shared Function str_repeat(str As String, num As Integer) As String
        Dim result As New StringBuilder
        For i As Integer = 1 To num
            result.Append(str)
        Next
        Return result.ToString
    End Function

    'return unique file name in form UUID (without extension)
    Public Shared Function get_uuid() As String
        Return System.Guid.NewGuid().ToString()
    End Function

    'return path to tmp filename WITHOUT extension
    Public Shared Function get_tmp_filename() As String
        Return Path.GetTempPath & "\tmp" & Utils.get_uuid()
    End Function

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
        If hattrs.ContainsKey("trend") Then trchar = hattrs("trend")
        If hattrs.ContainsKey("trword") Then trchar = hattrs("trword")

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
    Shared Function orderby_apply_sortdir(orderby As String, sortdir As String) As String
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
                    fld = fld & " desc"
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

End Class
