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

        ps("select_controllers") = New ArrayList
        For Each controller_name As String In _controllers()
            ps("select_controllers").add(New Hashtable From {{"id", controller_name}, {"iname", controller_name}})
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

    Public Function ResetCacheAction() As Hashtable
        fw.FLASH("success", "Application Caches cleared")

        FwCache.clear()
        db.clear_schema_cache()
        Dim pp = New ParsePage(fw)
        pp.clear_cache()

        fw.redirect(base_url)
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
        My.Computer.FileSystem.CopyDirectory(tpl_from, tpl_to, True)

        'replace in templates: DemoDynamic to Title
        'replace in url.html /Admin/DemosDynamic to controller_url
        Dim replacements As New Hashtable From {
                {"/Admin/DemosDynamic", controller_url},
                {"DemoDynamic", controller_title}
            }
        replaceInFiles(tpl_to, replacements)

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
        Dim config As Hashtable = Utils.jsonDecode(FW.get_file_content(config_file))
        If config Is Nothing Then config = New Hashtable

        Dim model = fw.model(model_name)
        db.connect()
        Dim fields = db.load_table_schema_full(model.table_name)
        Dim hfields As New Hashtable
        Dim sys_fields = Utils.qh("add_time add_users_id upd_time upd_users_id")

        Dim saveFields As New ArrayList
        Dim hFieldsMap As New Hashtable
        Dim showFields As New ArrayList
        Dim showFormFields As New ArrayList

        Dim isf_status As Integer = 0, isff_status As Integer = 0
        For Each fld In fields
            hfields(fld("name")) = fld
            hFieldsMap(fld("name")) = fld("name")

            Dim sf As New Hashtable
            Dim sff As New Hashtable
            Dim is_skip = False
            sf("field") = fld("name")
            sf("label") = fld("name")
            sf("type") = "plaintext"

            sff("field") = fld("name")
            sff("label") = fld("name")

            If fld("is_nullable") = "0" AndAlso fld("default") = "" Then sff("required") = True 'if not nullable and no default - required

            If Utils.f2int(fld("maxlen")) > 0 Then sff("maxlength") = Utils.f2int(fld("maxlen"))
            If fld("internal_type") = "varchar" Then
                If Utils.f2int(fld("maxlen")) = -1 Then 'large text
                    sf("type") = "markdown"
                    sff("type") = "textarea"
                    sff("rows") = 5
                    sff("class_control") = "markdown autoresize" 'or fw-html-editor or fw-html-editor-short
                Else
                    sff("type") = "input"
                End If
            ElseIf fld("internal_type") = "int" Then
                If Right(fld("name"), 3) = "_id" AndAlso fld("name") <> "dict_link_auto_id" Then 'TODO remove dict_link_auto_id
                    'TODO better detect if field has foreign key
                    'if link to other table - make type=select
                    Dim mname = _tablename2model(Left(fld("name"), Len(fld("name")) - 3))
                    If mname = "Parent" Then mname = model_name

                    sf("lookup_model") = mname
                    'sf("lookup_field") = "iname"

                    sff("type") = "select"
                    sff("lookup_model") = mname
                    sff("is_option0") = True
                    sff("class_contents") = "col-md-3"
                ElseIf fld("type") = "tinyint" Then
                    'make it as yes/no radio
                    sff("type") = "yesno"
                    sff("is_inline") = True
                Else
                    sff("type") = "number"
                    sff("min") = 0
                    sff("max") = 999999
                    sff("class_contents") = "col-md-3"
                End If
            ElseIf fld("internal_type") = "float" Then
                sff("type") = "number"
                sff("step") = 0.1
                sff("class_contents") = "col-md-3"
            ElseIf fld("internal_type") = "datetime" Then
                sf("type") = "date"
                sff("type") = "date_popup"
                sff("class_contents") = "col-md-3"
                'TODO distinguish between date and date with time
            Else
                'everything else - just input
                sff("type") = "input"
            End If

            If fld("is_identity") = "1" Then
                sff("type") = "group_id"
                sff.Remove("required")
            End If

            'special fields
            Select Case fld("name")
                Case "iname"
                    sff("validate") = "exists" 'unique field
                Case "att_id" 'Single attachment field - TODO better detect on foreign key to "att" table
                    sf("type") = "att"
                    sf("label") = "Attachment"
                    sf("class_contents") = "col-md-2"
                    sff.Remove("lookup_model")

                    sff("type") = "att_edit"
                    sff("label") = "Attachment"
                    sff("class_contents") = "col-md-3"
                    sff("att_category") = "general"
                    sff.Remove("class_contents")
                    sff.Remove("lookup_model")
                    sff.Remove("is_option0")
                Case "status"
                    sf("label") = "Status"
                    sf("lookup_tpl") = "/common/sel/status.sel"

                    sff("label") = "Status"
                    sff("type") = "select"
                    sff("lookup_tpl") = "/common/sel/status.sel"
                    sff("class_contents") = "col-md-3"
                Case "add_time"
                    sf("label") = "Added on"
                    sf("type") = "added"

                    sff("label") = "Added on"
                    sff("type") = "added"
                Case "upd_time"
                    sf("label") = "Updated on"
                    sf("type") = "updated"

                    sff("label") = "Updated on"
                    sff("type") = "updated"
                Case "add_users_id", "upd_users_id"
                    is_skip = True
                Case Else
                    'nothing else
            End Select

            If Not is_skip Then
                Dim sf_index = showFields.Add(sf)
                Dim sff_index = showFormFields.Add(sff)
                If fld("name") = "status" Then
                    isf_status = sf_index
                    isff_status = sff_index
                End If
            End If

            If fld("is_identity") = "1" OrElse sys_fields.Contains(fld("name")) Then Continue For
            saveFields.Add(fld("name"))
        Next

        'special case - "Lookup via Link Table" - could be multiple tables
        Dim rx_table_link = "^" & Regex.Escape(model.table_name) & "_(.+?)_link$"
        Dim tables = db.tables()
        Dim table_name_linked = ""
        Dim table_name_link = ""
        For Each table In tables
            Dim m = Regex.Match(table, rx_table_link)
            If m.Success Then
                table_name_linked = m.Groups(1).Value
                table_name_link = table

                If table_name_linked > "" Then
                    'if table "MODELTBL_TBL2_link" exists - add control for linked table
                    Dim sflink As New Hashtable From {
                        {"field", table_name_linked & "_link"},
                        {"label", "Linked " & table_name_linked},
                        {"type", "multi"},
                        {"lookup_model", _tablename2model(table_name_linked)},
                        {"table_link", table_name_link},
                        {"table_link_id_name", model.table_name & "_id"},
                        {"table_link_linked_id_name", table_name_linked & "_id"}
                    }
                    Dim sfflink As New Hashtable From {
                        {"field", table_name_linked & "_link"},
                        {"label", "Linked " & table_name_linked},
                        {"type", "multicb"},
                        {"lookup_model", _tablename2model(table_name_linked)},
                        {"table_link", table_name_link},
                        {"table_link_id_name", model.table_name & "_id"},
                        {"table_link_linked_id_name", table_name_linked & "_id"}
                    }

                    'add linked table before Status 
                    If isf_status > 0 Then
                        showFields.Insert(isf_status, sflink)
                        isf_status += 1
                    Else
                        showFields.Add(sflink)
                    End If

                    If isff_status > 0 Then
                        showFormFields.Insert(isff_status, sfflink)
                        isff_status += 1
                    Else
                        showFormFields.Add(sfflink)
                    End If
                End If

            End If
        Next
        'end special case for link table


        config("model") = model_name
        config("save_fields") = saveFields 'save all non-system
        config("save_fields_checkboxes") = ""
        config("search_fields") = "id" & If(hfields.ContainsKey("iname"), " iname", "") 'id iname
        config("list_sortdef") = If(hfields.ContainsKey("iname"), "iname asc", "id desc") 'either sort by iname or id
        config.Remove("list_sortmap") 'N/A in dynamic controller
        config.Remove("required_fields") 'not necessary in dynamic controller as controlled by showform_fields required attribute
        config("related_field_name") = "" 'TODO?
        config("list_view") = model.table_name
        config("view_list_defaults") = "id" & If(hfields.ContainsKey("iname"), " iname", "") & If(hfields.ContainsKey("add_time"), " add_time", "") & If(hfields.ContainsKey("status"), " status", "")
        config("view_list_map") = hFieldsMap 'fields to names
        config("view_list_custom") = "status"
        config("show_fields") = showFields
        config("showform_fields") = showFormFields

        'remove all commented items - name start with "#"
        For Each key In config.Keys.Cast(Of String).ToArray()
            If Left(key, 1) = "#" Then config.Remove(key)
        Next

        'Utils.jsonEncode(config) - can't use as it produces unformatted json string
        Dim config_str = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented)
        FW.set_file_content(config_file, config_str)

        fw.FLASH("controller_created", controller_name)
        fw.FLASH("controller_url", controller_url)
        fw.redirect(base_url)

    End Function

    Public Function ExtractControllerAction() As Hashtable
        Dim item = reqh("item")
        Dim controller_name = Trim(item("controller_name"))

        If Not _controllers.Contains(controller_name) Then Throw New ApplicationException("No controller found")

        Dim cInstance As FwDynamicController = Activator.CreateInstance(Type.GetType(controller_name, True))
        cInstance.init(fw)

        Dim tpl_to = LCase(cInstance.base_url)
        Dim tpl_path = fw.config("template") & tpl_to
        Dim config_file = tpl_path & "/config.json"
        Dim config As Hashtable = Utils.jsonDecode(FW.get_file_content(config_file))
        If config Is Nothing Then config = New Hashtable

        'extract ShowAction
        config("is_dynamic_show") = False
        Dim fitem As New Hashtable
        Dim fields = cInstance.prepareShowFields(fitem, New Hashtable)
        _makeValueTags(fields)

        Dim ps As New Hashtable
        ps("fields") = fields
        Dim parser As ParsePage = New ParsePage(fw)
        Dim content As String = parser.parse_page(tpl_to & "/show", "/common/form/show/extract/form.html", ps)
        content = Regex.Replace(content, "^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline) 'remove empty lines
        FW.set_file_content(tpl_path & "/show/form.html", content)

        'extract ShowAction
        config("is_dynamic_showform") = False
        fields = cInstance.prepareShowFormFields(fitem, New Hashtable)
        _makeValueTags(fields)
        ps = New Hashtable
        ps("fields") = fields
        parser = New ParsePage(fw)
        content = parser.parse_page(tpl_to & "/show", "/common/form/showform/extract/form.html", ps)
        content = Regex.Replace(content, "^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline) 'remove empty lines
        content = Regex.Replace(content, "&lt;~(.+?)&gt;", "<~$1>") 'unescape tags
        FW.set_file_content(tpl_path & "/showform/form.html", content)

        'TODO here - also modify controller code ShowFormAction to include listSelectOptions, multi_datarow, comboForDate, autocomplete name, etc...

        'now we could remove dynamic field definitions - uncomment if necessary
        'config.Remove("show_fields")
        'config.Remove("showform_fields")

        Dim config_str = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented)
        FW.set_file_content(config_file, config_str)

        fw.FLASH("success", "Controller " & controller_name & " extracted dynamic show/showfrom to static templates")
        fw.redirect(base_url)
    End Function

    'analyse database tables and create db.json describing entities, fields and relationships
    Public Function AnalyseDBAction() As Hashtable
        Dim ps As New Hashtable
        Dim item = reqh("item")
        Dim connstr As String = item("connstr") & ""

        Dim dbtype = "SQL"
        If connstr.Contains("OLE") Then dbtype = "OLE"

        'Try
        Dim db = New DB(fw)
        Dim conn = db.create_connection(connstr, dbtype)

        Dim entities = dbschema2entities(db)

        'save db.json
        saveJson(entities, fw.config("template") & "/db.json")

        conn.Close()
        'Catch ex As Exception
        '    fw.FLASH("error", ex.Message)
        '    fw.redirect(base_url)
        'End Try

        Return ps
    End Function

    Private Sub saveJson(data As Object, filename As String)
        Dim json_str = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented)
        FW.set_file_content(filename, json_str)
    End Sub

    Private Function dbschema2entities(db As DB) As ArrayList
        Dim result As New ArrayList
        'Access System tables:
        'MSysAccessStorage
        'MSysAccessXML
        'MSysACEs
        'MSysComplexColumns
        'MSysNameMap
        'MSysNavPaneGroupCategories
        'MSysNavPaneGroups
        'MSysNavPaneGroupToObjects
        'MSysNavPaneObjectIDs
        'MSysObjects
        'MSysQueries
        'MSysRelationships
        'MSysResources
        Dim tables = db.tables()
        For Each tblname In tables
            If InStr(tblname, "MSys", CompareMethod.Binary) = 1 Then Continue For

            'get table schema
            Dim tblschema = db.load_table_schema_full(tblname)
            'logger(tblschema)

            Dim table_entity As New Hashtable
            table_entity("table") = tblname
            table_entity("iname") = tblname 'TODO human table name
            table_entity("fields") = tableschema2entity(tblschema)
            result.Add(table_entity)
        Next

        'TODO for Access - get relationships from MSysRelationships

        Return result
    End Function

    Private Function tableschema2entity(schema As ArrayList) As ArrayList
        Dim result As New ArrayList(schema)

        For Each fldschema As Hashtable In schema
            'TODO fix field names: State/Province -> State_Province
            'result(fldschema("name")) = fldschema
        Next
        'result("xxxx") = "yyyy"
        'attrs used to build UI
        'name
        'default
        'maxlen
        'is_nullable
        'type
        'internal_type
        'is_identity

        Return result
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
        If content.Length = 0 Then Return

        For Each str As String In strings.Keys
            content = content.Replace(str, strings(str))
        Next

        FW.set_file_content(filepath, content)
    End Sub

    'demo_dicts => DemoDicts
    Private Function _tablename2model(table_name As String) As String
        Dim result As String = ""
        Dim pieces As String() = Split(table_name, "_")
        For Each piece As String In pieces
            result &= Utils.capitalize(piece)
        Next
        Return result
    End Function

    Private Sub _makeValueTags(fields As ArrayList)
        For Each def As Hashtable In fields
            Dim tag = "<~i[" & def("field") & "]"
            Select Case def("type")
                Case "date"
                    def("value") = tag & " date>"
                Case "date_long"
                    def("value") = tag & " date=""long"">"
                Case "float"
                    def("value") = tag & " number_format=""2"">"
                Case "markdown"
                    def("value") = tag & " markdown>"
                Case "noescape"
                    def("value") = tag & " noescape>"
                Case Else
                    def("value") = tag & ">"
            End Select
        Next
    End Sub

End Class
