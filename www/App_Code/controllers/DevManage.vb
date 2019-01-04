' Manage  controller for Developers
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018  Oleg Savchuk www.osalabs.com

Imports System.IO

Public Class DevManageController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        base_url = "/Dev/Manage"
    End Sub

    Public Function IndexAction() As Hashtable
        Dim ps As New Hashtable

        'table list
        Dim tables = db.tables()
        tables.Sort()
        ps("select_tables") = New ArrayList
        For Each table As String In tables
            ps("select_tables").add(New Hashtable From {{"id", table}, {"iname", table}})
        Next

        'models list - all clasess inherited from FwModel
        ps("select_models") = New ArrayList

        For Each model_name As String In _models()
            ps("select_models").add(New Hashtable From {{"id", model_name}, {"iname", model_name}})
        Next

        Return ps
    End Function

    Public Function DumpLogAction() As Hashtable
        Dim seek = reqi("seek")
        Dim logpath = fw.config("log")
        rw("Dump of last " & seek & " bytes of the site log")

        Dim fs = New FileStream(logpath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        fs.Seek(-seek, SeekOrigin.End)
        Dim sr = New StreamReader(fs)
        rw("<pre>")
        fw.resp.Write(sr.ReadToEnd())
        rw("</pre>")

        rw("end of dump")
        sr.Close()
    End Function

    Public Function CreateModelAction() As Hashtable
        Dim item = reqh("item")
        Dim table_name = Trim(item("table_name"))
        Dim model_name = Trim(item("model_name"))
        If table_name = "" OrElse model_name = "" OrElse _models.Contains(model_name) Then Throw New ApplicationException("No table name or no model name or model exists")

        'copy DemoDicts.vb to model_name.vb
        Dim path = fw.config("site_root") & "\App_Code\models"
        Dim mdemo = FW.get_file_content(path & "\DemoDicts.vb")
        If mdemo = "" Then Throw New ApplicationException("Can't open DemoDicts.vb")

        'replace: DemoDicts => ModelName, demo_dicts => table_name
        mdemo = mdemo.Replace("DemoDicts", model_name)
        mdemo = mdemo.Replace("demo_dicts", table_name)

        FW.set_file_content(path & "\" & model_name & ".vb", mdemo)

        fw.FLASH("success", model_name & ".vb model created")
        fw.redirect(base_url)
    End Function

    Public Function CreateControllerAction() As Hashtable
        Dim item = reqh("item")
        Dim model_name = Trim(item("model_name"))
        Dim controller_url = Trim(item("controller_url"))
        Dim controller_name = Replace(controller_url, "/", "")
        Dim controller_title = Trim(item("controller_title"))

        If model_name = "" OrElse controller_url = "" OrElse controller_title = "" Then Throw New ApplicationException("No model or no controller name or no title")
        If _controllers.Contains(controller_name) Then Throw New ApplicationException("Such controller already exists")

        'copy DemoDicts.vb to model_name.vb
        Dim path = fw.config("site_root") & "\App_Code\controllers"
        Dim mdemo = FW.get_file_content(path & "\AdminDemosDynamic.vb")
        If mdemo = "" Then Throw New ApplicationException("Can't open AdminDemosDynamic.vb")

        'replace: DemoDicts => ModelName, demo_dicts => table_name
        mdemo = mdemo.Replace("AdminDemosDynamic", controller_name)
        mdemo = mdemo.Replace("/Admin/DemosDynamic", controller_url)
        mdemo = mdemo.Replace("DemoDicts", model_name)
        mdemo = mdemo.Replace("Demos", model_name)

        FW.set_file_content(path & "\" & controller_name & ".vb", mdemo)

        'copy templates from /admin/demosdynamic to /controller/url
        Dim tpl_from = fw.config("template") & "/admin/demosdynamic"
        Dim tpl_to = fw.config("template") & controller_url.ToLower()
        My.Computer.FileSystem.CopyDirectory(tpl_from, tpl_to)

        'replace in templates: DemoDynamic to Title
        'replace in url.html /Admin/DemosDynamic to controller_url
        Dim replacements As New Hashtable From {
                {"/Admin/DemosDynamic", controller_url},
                {"DemoDynamic", controller_title}
            }
        replaceInFiles(tpl_to, replacements)

        'TODO
        'update config.json:
        ' save_fields - all fields from model table (except id and sytem add_time/user fields)
        ' save_fields_checkboxes - empty (TODO based on bit field?)
        ' list_view - model.table_name
        ' view_list_defaults - iname add_time status
        ' view_list_map
        ' view_list_custom - just status
        ' show_fields - all
        ' show_form_fields - all, analyse if:
        '   field NOT NULL and no default - required
        '   field has foreign key - add that table as dropdown
        Dim config_file = tpl_to & "/config.json"
        Dim config = Utils.jsonDecode(FW.get_file_content(config_file))
        If config Is Nothing Then config = New Hashtable

        Dim model = fw.model(model_name)
        Dim fields = db.load_table_schema_full(model.table_name)
        Dim hfields As New Hashtable
        Dim sys_fields = Utils.qh("add_time add_users_id upd_time upd_users_id")

        Dim alFields As New ArrayList
        For Each fld In fields
            hfields(fld("name")) = fld
            If fld("is_identity") = "1" OrElse sys_fields.Contains(fld("name")) Then Continue For
            alFields.Add(fld("name"))
        Next
        config("save_fields") = alFields
        config("save_fields_checkboxes") = ""
        config("search_fields") = "id" & If(hfields.ContainsKey("iname"), " iname", "") 'id iname
        config("list_sortdef") = If(hfields.ContainsKey("iname"), "iname asc", "id desc") 'either sort by iname or id
        config("list_sortmap") = "" 'N/A in dynamic controller
        config("related_field_name") = "" 'TODO?
        config("is_dynamic") = True
        config("list_view") = model.table_name
        config("view_list_defaults") = "id" & If(hfields.ContainsKey("iname"), " iname", "") & If(hfields.ContainsKey("add_time"), " add_time", "") & If(hfields.ContainsKey("status"), " status", "")
        config("view_list_map") = "" 'TODO fields to names?
        config("view_list_custom") = "status"
        config("show_fields") = New ArrayList 'TODO
        config("showform_fields") = New ArrayList 'TODO

        FW.set_file_content(config_file, Utils.jsonEncode(config))

        fw.FLASH("success", controller_name & ".vb controller created, " & controller_url & ", templates copied, config.json updated")
        fw.redirect(base_url)

    End Function


    Private Function _models() As IOrderedEnumerable(Of String)
        Dim baseType = GetType(FwModel)
        Dim assembly = baseType.Assembly
        Return From t In assembly.GetTypes()
               Where t.IsSubclassOf(baseType)
               Select t.Name
               Order By Name
    End Function

    Private Function _controllers() As IOrderedEnumerable(Of String)
        Dim baseType = GetType(FwController)
        Dim assembly = baseType.Assembly
        Return From t In assembly.GetTypes()
               Where t.IsSubclassOf(baseType)
               Select t.Name
               Order By Name
    End Function

    'replaces strings in all files under defined dir
    'RECURSIVE!
    Private Sub replaceInFiles(dir As String, strings As Hashtable)
        For Each filename As String In Directory.GetFiles(dir)
            replaceInFile(filename, strings)
        Next

        'dive into dirs
        For Each foldername As String In Directory.GetDirectories(dir)
            replaceInFiles(foldername, strings)
        Next
    End Sub

    Private Sub replaceInFile(filepath As String, strings As Hashtable)
        Dim content = FW.get_file_content(filepath)
        If content.Length = "" Then Return

        For Each str As String In strings.Keys
            content.Replace(str, strings(str))
        Next

        FW.set_file_content(filepath, content)
    End Sub

End Class
