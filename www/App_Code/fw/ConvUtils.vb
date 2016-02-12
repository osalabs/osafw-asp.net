Imports System.IO

'see also http://stackoverflow.com/questions/1331926/calling-wkhtmltopdf-to-generate-pdf-from-html/1698839#1698839

Public Class ConvUtils

    'parse template and generate pdf
    'Note: set IS_PRINT_MODE=True hf var which is become available in templates
    'if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    'if out_filename cotains "\" or "/" - save pdf file to this path
    'options:
    '  landscape = True - will produce landscape output
    Public Shared Function parse_page_pdf(fw As FW, ByRef bdir As String, ByRef tpl_name As String, ByRef hf As Hashtable, Optional out_filename As String = "", Optional options As Hashtable = Nothing) As String
        Dim parser As ParsePage = New ParsePage(fw)
        hf("IS_PRINT_MODE") = True
        Dim html_data As String = parser.parse_page(bdir, tpl_name, hf)

        html_data = _replace_specials(html_data)

        Dim html_file As String = Utils.get_tmp_filename() & ".html"
        Dim pdf_file As String = Utils.get_tmp_filename() & ".pdf"
        'fw.logger("INFO", "html file = " & html_file)
        'fw.logger("INFO", "pdf file = " & pdf_file)

        'remove_old_files()
        fw.set_file_content(html_file, html_data)

        If out_filename = "" OrElse Not Regex.IsMatch(out_filename, "[\/\\]") Then
            html2pdf(fw, html_file, pdf_file, options)

            If out_filename = "" Then out_filename = "output"
            fw.logger("INFO", "sending file response  = " & pdf_file & " to " & out_filename & ".pdf")
            fw.file_response(pdf_file, out_filename & ".pdf")
            Utils.cleanup_tmp_files() 'this will cleanup temporary .pdf, can't delete immediately as file_response may not yet finish transferring file
        Else
            html2pdf(fw, html_file, out_filename, options)
        End If
        'remove tmp html file
        File.Delete(html_file)

        Return html_data
    End Function

    '!uses CONF var FW.config("pdf_converter") for converted command line
    '!and FW.config("pdf_converter_args") - MUST include %IN %OUT which will be replaced by input and output file paths accordingly
    'TODO: example: FW.config("html_converter_args")=" -po Landscape" - for landscape mode
    'all params for TotalHTMLConverter: http://www.coolutils.com/help/TotalHTMLConverter/Commandlineparameters.php
    'options:
    '  landscape = True - will produce landscape output
    Public Shared Sub html2pdf(fw As FW, ByVal htmlfile As String, ByVal filename As String, Optional options As Hashtable = Nothing)
        If htmlfile.Length < 1 Or filename.Length < 1 Then Throw New ApplicationException("Wrong filename")
        Dim info As New System.Diagnostics.ProcessStartInfo
        Dim process As New System.Diagnostics.Process

        Dim cmdline As String = FwConfig.settings("pdf_converter_args")
        cmdline = cmdline.Replace("%IN", """" & htmlfile & """")
        cmdline = cmdline.Replace("%OUT", """" & filename & """")
        If Not IsNothing(options) AndAlso Utils.f2bool(options("landscape")) = True Then
            cmdline = " -O Landscape " & cmdline
        End If
        info.FileName = FwConfig.settings("pdf_converter")
        info.Arguments = cmdline

        fw.logger("INFO", info.FileName & " " & info.Arguments)
        process.StartInfo = info
        process.Start()
        process.WaitForExit()
        process.Close()
    End Sub

    'TODO - currently it just parse html and save it under .doc extension (Word capable opening it), but need redo with real converter
    'parse template and generate doc
    'if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    'if out_filename cotains "\" or "/" - save pdf file to this path
    Public Shared Function parse_page_doc(fw As FW, ByRef bdir As String, ByRef tpl_name As String, ByRef hf As Hashtable, Optional out_filename As String = "") As String
        Dim parser As ParsePage = New ParsePage(fw)
        Dim html_data As String = parser.parse_page(bdir, tpl_name, hf)

        html_data = _replace_specials(html_data)

        Dim html_file As String = Utils.get_tmp_filename() & ".html"
        Dim doc_file As String = Utils.get_tmp_filename() & ".doc"
        'fw.logger("INFO", "html file = " & html_file)
        'fw.logger("INFO", "doc file = " & doc_file)

        'remove_old_files()
        'TODO fw.set_file_content(html_file, html_data)
        'TEMPORARY - store html right to .doc file
        fw.set_file_content(doc_file, html_data)

        If out_filename = "" OrElse Not Regex.IsMatch(out_filename, "[\/]") Then
            'TODO html2doc(fw, html_file, doc_file)

            If out_filename = "" Then out_filename = "output"
            fw.logger("INFO", "sending file response  = " & doc_file & " to " & out_filename & ".doc")
            fw.file_response(doc_file, out_filename & ".doc")
            Utils.cleanup_tmp_files() 'this will cleanup temporary .pdf, can't delete immediately as file_response may not yet finish transferring file
        Else
            'TODO html2doc(fw, html_file, out_filename)
            File.Delete(out_filename)
            File.Move(doc_file, out_filename)
        End If
        'remove tmp html file
        File.Delete(html_file)

        Return html_data
    End Function

    'using http://www.coolutils.com/TotalHTMLConverterX
    'params http://www.coolutils.com/help/TotalHTMLConverter/Commandlineparameters.php
    Public Shared Sub html2xls(fw As FW, ByVal htmlfile As String, ByVal xlsfile As String)
        If htmlfile.Length < 1 Or xlsfile.Length < 1 Then Throw New ApplicationException("Wrong filename")
        Dim info As New System.Diagnostics.ProcessStartInfo
        Dim process As New System.Diagnostics.Process

        info.FileName = FW.config("html_converter")
        info.Arguments = """" & htmlfile & """ """ & xlsfile & """ -c xls -AutoSize"
        process.StartInfo = info
        process.Start()
        process.WaitForExit()
        process.Close()
    End Sub

    'parse template and generate xls
    'Note: set IS_PRINT_MODE=True hf var which is become available in templates
    'if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    'if out_filename cotains "\" or "/" - save pdf file to this path
    Public Shared Function parse_page_xls(fw As FW, ByRef bdir As String, ByRef tpl_name As String, ByRef hf As Hashtable, Optional out_filename As String = "") As String
        Dim parser As ParsePage = New ParsePage(fw)
        hf("IS_PRINT_MODE") = True
        Dim html_data As String = parser.parse_page(bdir, tpl_name, hf)

        html_data = _replace_specials(html_data)

        Dim html_file As String = Utils.get_tmp_filename() & ".html"
        Dim xls_file As String = Utils.get_tmp_filename() & ".xls"
        fw.logger("INFO", "html file = " & html_file)
        fw.logger("INFO", "xls file = " & xls_file)

        'remove_old_files()
        fw.set_file_content(html_file, html_data)

        If out_filename = "" OrElse Not Regex.IsMatch(out_filename, "[\/\\]") Then
            html2xls(fw, html_file, xls_file)

            If out_filename = "" Then out_filename = "output"
            fw.logger("INFO", "sending file response  = " & xls_file & " to " & out_filename & ".xls")
            fw.file_response(xls_file, out_filename & ".xls", "application/vnd.ms-excel")
            Utils.cleanup_tmp_files() 'this will cleanup temporary .pdf, can't delete immediately as file_response may not yet finish transferring file
        Else
            html2xls(fw, html_file, out_filename)
        End If
        'remove tmp html file
        File.Delete(html_file)

        Return html_data
    End Function

    'simple version of parse_page_xls - i.e. it's usual html file, just output as xls (Excel opens it successfully, however displays a warning)
    Public Shared Function parse_page_xls_simple(fw As FW, ByRef bdir As String, ByRef tpl_name As String, ByRef hf As Hashtable, Optional out_filename As String = "") As String
        Dim parser As ParsePage = New ParsePage(fw)
        hf("IS_PRINT_MODE") = True
        Dim html_data As String = parser.parse_page(bdir, tpl_name, hf)

        html_data = _replace_specials(html_data)

        If out_filename = "" OrElse Not Regex.IsMatch(out_filename, "[\/\\]") Then
            If out_filename = "" Then out_filename = "output"
            'out to browser
            fw.resp.AddHeader("Content-type", "application/vnd.ms-excel")
            fw.resp.AddHeader("Content-Disposition", "attachment; filename=""" & out_filename & ".xls""")
            fw.resp.Write(html_data)
        Else
            FW.set_file_content(out_filename, html_data)
        End If

        Return html_data
    End Function

    'replace couple special chars
    Private Shared Function _replace_specials(html_data As String) As String
        html_data = html_data.Replace(Chr(153), "<sup><small>TM</small></sup>")
        html_data = html_data.Replace(Chr(174), "<sup><small>R</small></sup>")
        Return html_data
    End Function
End Class
