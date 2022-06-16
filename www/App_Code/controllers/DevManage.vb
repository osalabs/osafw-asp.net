' Manage  controller for Developers
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018  Oleg Savchuk www.osalabs.com

Imports System.Activities.Statements
Imports System.CodeDom
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices.WindowsRuntime

Public Class DevManageController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Const DB_SQL_PATH = "/App_Data/sql/database.sql" 'relative to site_root
    Const DB_JSON_PATH = "/dev/db.json"
    Const ENTITIES_PATH = "/dev/entities.txt"
    Const FW_TABLES = "att_categories att att_table_link users settings spages events event_log lookup_manager_tables user_views user_lists user_lists_items menu_items"

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        base_url = "/Dev/Manage"
    End Sub

    Public Function IndexAction() As Hashtable
        Dim ps As New Hashtable

        'table and views list
        Dim tables = db.tables()
        Dim views = db.views()
        tables.AddRange(views)
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

    Public Sub DumpLogAction()
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
    End Sub

    Public Sub ResetCacheAction()
        fw.FLASH("success", "Application Caches cleared")

        FwCache.clear()
        db.clear_schema_cache()
        Dim pp = New ParsePage(fw)
        pp.clear_cache()

        fw.redirect(base_url)
    End Sub

    Public Sub DeleteMenuItemsAction()
        fw.FLASH("success", "Menu Items cleared")

        db.del("menu_items", New Hashtable)
        FwCache.remove("menu_items")

        fw.redirect(base_url)
    End Sub

    Public Sub ReloadSessionAction()
        fw.FLASH("success", "Session Reloaded")

        fw.model(Of Users).reloadSession()

        fw.redirect(base_url)
    End Sub

    Public Function ShowDBUpdatesAction() As Hashtable
        Dim ps As New Hashtable

        'show list of available db updates
        Dim updates_root = fw.config("site_root") & "\App_Data\sql\updates"
        If IO.Directory.Exists(updates_root) Then
            Dim files() As String = IO.Directory.GetFiles(updates_root)

            Dim rows As New ArrayList
            For Each file As String In files
                rows.Add(New Hashtable From {{"filename", IO.Path.GetFileName(file)}})
            Next
            ps("rows") = rows
        Else
            ps("is_nodir") = True
            ps("updates_root") = updates_root
        End If

        Return ps
    End Function

    Public Sub SaveDBUpdatesAction()
        checkXSS()

        Dim is_view_only = (reqi("ViewsOnly") = 1)
        Dim ctr = 0

        Try
            If Not is_view_only Then
                'apply selected updates
                Dim updates_root = fw.config("site_root") & "\App_Data\sql\updates"
                Dim item = reqh("item")
                For Each filename As String In item.Keys
                    Dim filepath = updates_root & "\" & filename
                    rw("applying: " & filepath)
                    ctr += exec_multi_sql(FW.get_file_content(filepath))
                Next
                rw("Done, " & ctr & " statements executed")
            End If

            'refresh views
            ctr = 0
            Dim views_file = fw.config("site_root") & "\App_Data\sql\views.sql"
            rw("Applying views file: " & views_file)
            'for views - ignore errors
            ctr = exec_multi_sql(FW.get_file_content(views_file), True)
            rw("Done, " & ctr & " statements executed")

            rw("<b>All Done</b>")

        Catch ex As Exception
            rw("got an error")
            rw("<span style='color:red'>" & ex.Message & "</span>")
        End Try

        'and last - reset db schema cache
        FwCache.clear()
        db.clear_schema_cache()
    End Sub
    'TODO move these functions to DB?
    Private Function exec_multi_sql(sql As String, Optional is_ignore_errors As Boolean = False) As Integer
        Dim result = 0
        'launch the query
        Dim sql1 As String = strip_comments_sql(sql)
        Dim asql() As [String] = split_multi_sql(sql)
        For Each sqlone As String In asql
            sqlone = Trim(sqlone)
            If sqlone > "" Then
                If is_ignore_errors Then
                    Try
                        db.exec(sqlone)
                        result += 1
                    Catch ex As Exception
                        rw("<span style='color:red'>" & ex.Message & "</span>")
                    End Try
                Else
                    db.exec(sqlone)
                    result += 1
                End If
            End If
        Next
        Return result
    End Function
    Private Function strip_comments_sql(ByVal sql As String) As String
        Return Regex.Replace(sql, "/\*.+?\*/", " ", RegexOptions.Singleline)
    End Function
    Private Function split_multi_sql(ByVal sql As String) As String()
        Return Regex.Split(sql, ";[\n\r](?:GO[\n\r]+)[\n\r]*|[\n\r]+GO[\n\r]+")
    End Function



    Public Sub CreateModelAction()
        Dim item = reqh("item")
        Dim table_name = Trim(item("table_name"))
        Dim model_name = Trim(item("model_name"))

        Dim entity As New Hashtable From {
                {"table", table_name},
                {"model_name", model_name},
                {"db_config", ""}
            }
        createModel(entity)

        fw.FLASH("success", model_name & ".vb model created")
        fw.redirect(base_url)
    End Sub

    Public Sub CreateControllerAction()
        Dim item = reqh("item")
        Dim model_name = Trim(item("model_name"))
        Dim controller_url = Trim(item("controller_url"))
        Dim controller_title = Trim(item("controller_title"))

        'emulate entity
        Dim entity = New Hashtable From {
                    {"model_name", model_name},
                    {"controller_url", controller_url},
                    {"controller_title", controller_title},
                    {"table", fw.model(model_name).table_name}
                }
        'table = Utils.name2fw(model_name) - this is not always ok

        createController(entity, Nothing)
        Dim controller_name = Replace(entity("controller_url"), "/", "")

        fw.FLASH("controller_created", controller_name)
        fw.FLASH("controller_url", entity("controller_url"))
        fw.redirect(base_url)
    End Sub

    Public Sub ExtractControllerAction()
        Dim item = reqh("item")
        Dim controller_name = Trim(item("controller_name"))

        If Not _controllers.Contains(controller_name) Then Throw New ApplicationException("No controller found")

        Dim cInstance As FwDynamicController = Activator.CreateInstance(Type.GetType(controller_name, True))
        cInstance.init(fw)

        Dim tpl_to = LCase(cInstance.base_url)
        Dim tpl_path = fw.config("template") & tpl_to
        Dim config_file = tpl_path & "/config.json"
        Dim config = loadJson(Of Hashtable)(config_file)

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

        ''TODO here - also modify controller code ShowFormAction to include listSelectOptions, multi_datarow, comboForDate, autocomplete name, etc...

        'now we could remove dynamic field definitions - uncomment if necessary
        'config.Remove("show_fields")
        'config.Remove("showform_fields")

        saveJson(config, config_file)

        fw.FLASH("success", "Controller " & controller_name & " extracted dynamic show/showfrom to static templates")
        fw.redirect(base_url)
    End Sub

    'analyse database tables and create db.json describing entities, fields and relationships
    Public Function AnalyseDBAction() As Hashtable
        Dim ps As New Hashtable
        Dim item = reqh("item")
        Dim connstr As String = item("connstr") & ""

        Dim dbtype = "SQL"
        If connstr.Contains("OLE") Then dbtype = "OLE"

        'Try
        Dim db = New DB(fw, New Hashtable From {{"connection_string", connstr}, {"type", dbtype}})

        Dim entities = dbschema2entities(db)

        'save db.json
        saveJson(entities, fw.config("template") & DB_JSON_PATH)

        db.disconnect()
        fw.FLASH("success", "template" & DB_JSON_PATH & " created")

        'Catch ex As Exception
        '    fw.FLASH("error", ex.Message)
        '    fw.redirect(base_url)
        'End Try

        fw.redirect(base_url)

        Return ps
    End Function


    '************************* APP CREATION Actions
    '************************* DB Analyzer
    Public Function DBAnalyzerAction() As Hashtable
        Dim ps As New Hashtable
        Dim dbsources As New ArrayList

        For Each dbname As String In fw.config("db").Keys
            dbsources.Add(New Hashtable From {
                            {"id", dbname},
                            {"iname", dbname}
                          })
        Next

        ps("dbsources") = dbsources
        Return ps
    End Function

    Public Sub DBAnalyzerSaveAction()
        Dim item = reqh("item")
        Dim dbname As String = item("db") & ""
        Dim dbconfig = fw.config("db")(dbname)
        If dbconfig Is Nothing Then Throw New ApplicationException("Wrong DB selection")

        createDBJsonFromExistingDB(dbname)
        fw.FLASH("success", "template" & DB_JSON_PATH & " created")

        fw.redirect(base_url & "/(AppCreator)")
    End Sub

    Public Function EntityBuilderAction() As Hashtable
        Dim ps As New Hashtable

        Dim entities_file = fw.config("template") & ENTITIES_PATH
        Dim item As New Hashtable
        item("entities") = FW.get_file_content(entities_file)
        ps("i") = item

        Return ps
    End Function

    Public Sub EntityBuilderSaveAction()
        Dim item = reqh("item")
        Dim is_create_all = reqi("DoMagic") = 1

        Dim entities_file = fw.config("template") & ENTITIES_PATH
        FW.set_file_content(entities_file, item("entities"))

        Try
            If is_create_all Then
                'TODO create db.json, db, models/controllers
                createDBJsonFromText(item("entities"))
                createDBFromDBJson()
                createDBSQLFromDBJson()
                createModelsAndControllersFromDBJson()

                fw.FLASH("success", "Application created")
            Else
                'create db.json only
                createDBJsonFromText(item("entities"))
                fw.FLASH("success", "template" & DB_JSON_PATH & " created")
                fw.redirect(base_url & "/(DBInitializer)")
            End If
        Catch ex As ApplicationException
            fw.FLASH("error", ex.Message)
        End Try

        fw.redirect(base_url & "/(EntityBuilder)")
    End Sub

    Public Function DBInitializerAction() As Hashtable
        Dim ps As New Hashtable

        Dim config_file = fw.config("template") & DB_JSON_PATH
        Dim entities = loadJson(Of ArrayList)(config_file)

        ps("tables") = entities

        Return ps
    End Function

    Public Sub DBInitializerSaveAction()
        Dim is_sql_only = reqi("DoSQL") = 1

        If is_sql_only Then
            createDBSQLFromDBJson()
            fw.FLASH("success", DB_SQL_PATH & " created")

            fw.redirect(base_url & "/(DBInitializer)")
        Else
            createDBFromDBJson()
            fw.FLASH("success", "DB tables created")

            fw.redirect(base_url & "/(AppCreator)")
        End If
    End Sub

    Public Function AppCreatorAction() As Hashtable
        'reload session, so sidebar menu will be updated
        If reqs("reload") > "" Then fw.model(Of Users).reloadSession()

        Dim ps As New Hashtable

        'tables
        Dim config_file = fw.config("template") & DB_JSON_PATH
        Dim entities = loadJson(Of ArrayList)(config_file)

        Dim models = _models()
        Dim controllers = _controllers()

        For Each entity As Hashtable In entities
            entity("is_model_exists") = _models.Contains(entity("model_name"))
            entity("controller_name") = Replace(entity("controller_url"), "/", "")
            entity("is_controller_exists") = _controllers.Contains(entity("controller_name") & "Controller")
        Next

        ps("entities") = entities
        Return ps
    End Function

    Public Sub AppCreatorSaveAction()
        Dim item = reqh("item")

        Dim config_file = fw.config("template") & DB_JSON_PATH
        Dim entities = loadJson(Of ArrayList)(config_file)

        'go thru entities and:
        'update checked rows for any user input (like model name changed)
        Dim is_updated = False
        For Each entity As Hashtable In entities
            Dim key = entity("fw_name") & "#"
            If item.ContainsKey(key & "is_model") Then
                'create model
                If item(key & "model_name") > "" AndAlso entity("model_name") <> item(key & "model_name") Then
                    is_updated = True
                    entity("model_name") = item(key & "model_name")
                End If
                Me.createModel(entity)
            End If

            If item.ContainsKey(key & "is_controller") Then
                'create controller (model must exists)
                If item(key & "controller_name") > "" AndAlso entity("controller_name") <> item(key & "controller_name") Then
                    is_updated = True
                    entity("controller_name") = item(key & "controller_name")
                End If
                If item(key & "controller_title") > "" AndAlso entity("controller_title") <> item(key & "controller_title") Then
                    is_updated = True
                    entity("controller_title") = item(key & "controller_title")
                End If
                If Not entity.ContainsKey("controller_is_dynamic_show") OrElse Utils.f2bool(entity("controller_is_dynamic_show")) <> (item(key & "coview") > "") Then
                    is_updated = True
                    entity("controller_is_dynamic_show") = item(key & "coview") > ""
                End If
                If Not entity.ContainsKey("controller_is_dynamic_showform") OrElse Utils.f2bool(entity("controller_is_dynamic_showform")) <> (item(key & "coedit") > "") Then
                    is_updated = True
                    entity("controller_is_dynamic_showform") = item(key & "coedit") > ""
                End If
                If Not entity.ContainsKey("controller_is_lookup") OrElse Utils.f2bool(entity("controller_is_lookup")) <> (item(key & "colookup") > "") Then
                    is_updated = True
                    entity("controller_is_lookup") = item(key & "colookup") > ""
                End If
                Me.createController(entity, entities)
            End If
        Next

        'save db.json if there are any changes
        If is_updated Then saveJson(entities, config_file)

        fw.FLASH("success", "App build successfull")
        fw.redirect(base_url & "/(AppCreator)?reload=1")

    End Sub


    '****************************** PRIVATE HELPERS (move to Dev model?)

    'load json
    Private Function loadJson(Of T As {New})(filename As String) As T
        Dim result As T
        result = Utils.jsonDecode(FW.get_file_content(filename))
        If result Is Nothing Then result = New T()
        Return result
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
            table_entity("db_config") = db.db_name
            table_entity("table") = tblname
            table_entity("fw_name") = Utils.name2fw(tblname) 'new table name using fw standards
            table_entity("iname") = Utils.name2human(tblname) 'human table name
            table_entity("fields") = tableschema2fields(tblschema)
            table_entity("foreign_keys") = db.get_foreign_keys(tblname)

            table_entity("model_name") = Me._tablename2model(table_entity("fw_name")) 'potential Model Name
            table_entity("controller_url") = "/Admin/" & table_entity("model_name") 'potential Controller URL/Name/Title
            table_entity("controller_title") = Utils.name2human(table_entity("model_name"))

            'set is_fw flag - if it's fw compatible (contains id,iname,status,add_time,add_users_id)
            Dim fields = array2hashtable(table_entity("fields"), "name")
            'AndAlso fields.Contains("iname") 
            table_entity("is_fw") = fields.Contains("id") AndAlso fields.Contains("status") AndAlso fields.Contains("add_time") AndAlso fields.Contains("add_users_id")
            result.Add(table_entity)
        Next

        Return result
    End Function

    Private Function tableschema2fields(schema As ArrayList) As ArrayList
        Dim result As New ArrayList(schema)

        For Each fldschema As Hashtable In schema
            'prepare system/human field names: State/Province -> state_province
            'If fldschema("is_identity") = 1 Then
            '    fldschema("fw_name") = "id" 'identity fields always id
            '    fldschema("iname") = "ID"
            'Else
            fldschema("fw_name") = Utils.name2fw(fldschema("name"))
            fldschema("iname") = Utils.name2human(fldschema("name"))
            'End If
        Next
        'result("xxxx") = "yyyy"
        'attrs used to build UI
        'name => iname
        'default
        'maxlen
        'is_nullable
        'type
        'fw_type
        'is_identity

        Return result
    End Function


    'convert array of hashtables to hashtable of hashtables using key
    Private Function array2hashtable(arr As ArrayList, key As String) As Hashtable
        Dim result As New Hashtable
        For Each item As Hashtable In arr
            result(item(key)) = item
        Next
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
    'TODO actually go thru models and find model with table_name
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

    Private Sub createDBJsonFromText(entities_text As String)
        Dim entities = New ArrayList

        Dim lines = Regex.Split(entities_text, "[\n\r]+")
        Dim table_entity As Hashtable = Nothing
        For Each line As String In lines
            line = Regex.Replace(line, "#.+$", "") 'remove any human comments
            If Trim(line) = "" Then Continue For
            fw.logger(line)

            If line.Substring(0, 1) = "-" Then
                'if new entity - add system fields to previous entity
                If table_entity IsNot Nothing Then table_entity("fields").AddRange(defaultFieldsAfter())

                'new entity
                table_entity = New Hashtable
                entities.Add(table_entity)

                line = Regex.Replace(line, "^-\s*", "") 'remove prefix 'human table name
                Dim parts = Regex.Split(line, "\s+")
                Dim table_name = parts(0) 'name is first 

                table_entity("db_config") = "" 'main
                table_entity("iname") = Utils.name2human(table_name)
                table_entity("table") = Utils.name2fw(table_name)
                If isFwTableName(table_entity("table")) Then Throw New ApplicationException("Cannot have table name " & table_entity("table"))

                table_entity("fw_name") = Utils.name2fw(table_name) 'new table name using fw standards

                table_entity("model_name") = Me._tablename2model(table_entity("fw_name")) 'potential Model Name
                table_entity("controller_url") = "/Admin/" & table_entity("model_name") 'potential Controller URL/Name/Title
                table_entity("controller_title") = Utils.name2human(table_entity("model_name"))
                If Regex.IsMatch(line, "\blookup\b") Then
                    table_entity("controller_is_lookup") = True
                End If
                table_entity("is_fw") = True
                'add default system fields
                table_entity("fields") = New ArrayList(defaultFields())
                table_entity("foreign_keys") = New ArrayList

            Else
                'entity field
                If table_entity Is Nothing Then Continue For 'skip if table_entity is not initialized yet
                If line.Substring(0, 3) <> "  -" Then Continue For 'skip strange things

                line = Regex.Replace(line, "^  -\s*", "") 'remove prefix 
                Dim parts = Regex.Split(line, "\s+")
                Dim field_name = parts(0) 'name is first 

                'special - *Address -> set of address fields
                If Regex.IsMatch(field_name, "Address$", RegexOptions.IgnoreCase) Then
                    table_entity("fields").AddRange(addressFields(field_name))
                    Continue For
                End If

                Dim field As New Hashtable
                table_entity("fields").Add(field)

                'check if field is foreign key
                If Right(field_name, 3) = ".id" Then
                    'this is foreign key field
                    Dim fk As New Hashtable
                    table_entity("foreign_keys").Add(fk)

                    fk("pk_table") = Utils.name2fw(Regex.Replace(field_name, "\.id$", ""))  'Customers.id => customers
                    fk("pk_column") = "id"
                    field_name = fk("pk_table") & "_id"
                    fk("column") = field_name

                    field("fw_type") = "int"
                    field("fw_type") = "int"
                End If

                field("name") = field_name
                field("iname") = Utils.name2human(field_name)
                field("fw_name") = Utils.name2fw(field_name)
                field("is_identity") = 0

                field("is_nullable") = IIf(Regex.IsMatch(line, "\bNULL\b"), 1, 0)
                field("numeric_precision") = Nothing
                field("maxlen") = Nothing
                'detect type if not yet set by foreigh key
                If field("fw_type") = "" Then
                    field("fw_type") = "varchar"
                    field("fw_subtype") = "nvarchar"
                    Dim m = Regex.Match(line, "varchar\((.+?)\)") 'detect varchar(LEN|MAX)
                    If m.Success Then
                        If m.Groups(1).Value = "MAX" OrElse Utils.f2int(m.Groups(1).Value) > 255 Then
                            field("maxlen") = -1
                        Else
                            field("maxlen") = Utils.f2int(m.Groups(1).Value)
                        End If
                    ElseIf Regex.IsMatch(line, "\bint\b", RegexOptions.IgnoreCase) Then
                        field("numeric_precision") = 10
                        field("fw_type") = "int"
                        field("fw_subtype") = "int"
                    ElseIf Regex.IsMatch(line, "\btinyint\b", RegexOptions.IgnoreCase) Then
                        field("numeric_precision") = 3
                        field("fw_type") = "int"
                        field("fw_subtype") = "tinyint"
                    ElseIf Regex.IsMatch(line, "\bbit\b", RegexOptions.IgnoreCase) Then
                        field("numeric_precision") = 1
                        field("fw_type") = "int"
                        field("fw_subtype") = "bit"
                    ElseIf Regex.IsMatch(line, "\bfloat\b", RegexOptions.IgnoreCase) Then
                        field("numeric_precision") = 53
                        field("fw_type") = "float"
                        field("fw_subtype") = "float"
                    ElseIf Regex.IsMatch(line, "\bdate\b", RegexOptions.IgnoreCase) Then
                        field("fw_type") = "datetime"
                        field("fw_subtype") = "date"
                    ElseIf Regex.IsMatch(line, "\bdatetime\b", RegexOptions.IgnoreCase) Then
                        field("fw_type") = "datetime"
                        field("fw_subtype") = "datetime2"
                    Else
                        'not type specified
                        'additionally detect date field from name
                        If Regex.IsMatch(field("name"), "Date$", RegexOptions.IgnoreCase) Then
                            field("fw_type") = "datetime"
                            field("fw_subtype") = "date"
                        Else
                            'just a default varchar(255)
                            field("maxlen") = 255
                        End If
                    End If

                    'default
                    field("default") = Nothing
                    m = Regex.Match(line, "\bdefault\s+\((.+)\)") 'default (VALUE_HERE)
                    If m.Success Then
                        field("default") = m.Groups(1).Value
                    Else
                        'no default set - then for nvarchar set empty strin gdefault
                        If field("fw_type") = "varchar" Then
                            field("default") = ""
                        End If
                    End If
                End If

            End If
        Next
        'add system fields to last entity
        If table_entity IsNot Nothing Then table_entity("fields").AddRange(defaultFieldsAfter())

        'save db.json
        saveJson(entities, fw.config("template") & DB_JSON_PATH)
    End Sub

    Private Sub createDBJsonFromExistingDB(dbname As String)
        Dim db = New DB(fw, fw.config("db")(dbname), dbname)

        Dim entities = dbschema2entities(db)

        'save db.json
        saveJson(entities, fw.config("template") & DB_JSON_PATH)

        db.disconnect()
    End Sub

    Private Sub createDBFromDBJson()
        Dim config_file = fw.config("template") & DB_JSON_PATH
        Dim entities = loadJson(Of ArrayList)(config_file)

        'drop all FKs we created before, so we'll be able to drop tables later
        Dim fks = db.array("SELECT fk.name, o.name as table_name FROM sys.foreign_keys fk, sys.objects o where fk.is_system_named=0 and o.object_id=fk.parent_object_id")
        For Each fk As Hashtable In fks
            db.exec("ALTER TABLE " & db.q_ident(fk("table_name")) & " DROP CONSTRAINT " & db.q_ident(fk("name")))
        Next

        For Each entity In entities
            Dim sql = entity2SQL(entity)
            'create db tables directly in db

            Try
                db.exec("DROP TABLE " & db.q_ident(entity("table")))
            Catch ex As Exception
                logger(ex.Message)
                'just ignore drop exceptions
            End Try

            db.exec(sql)
        Next
    End Sub

    Private Sub createDBSQLFromDBJson()
        Dim config_file = fw.config("template") & DB_JSON_PATH
        Dim entities = loadJson(Of ArrayList)(config_file)

        Dim database_sql = ""
        For Each entity In entities
            Dim sql = entity2SQL(entity)
            'only create App_Data/database.sql
            'add drop
            database_sql &= "DROP TABLE " & db.q_ident(entity("table")) & ";" & vbCrLf
            database_sql &= sql & ";" & vbCrLf & vbCrLf
        Next

        Dim sql_file = fw.config("site_root") & DB_SQL_PATH
        FW.set_file_content(sql_file, database_sql)
    End Sub

    Private Sub createModelsAndControllersFromDBJson()
        Dim config_file = fw.config("template") & DB_JSON_PATH
        Dim entities = loadJson(Of ArrayList)(config_file)

        For Each entity In entities
            Me.createModel(entity)
            Me.createController(entity, entities)
        Next
    End Sub


    Private Sub createModel(entity As Hashtable)
        Dim table_name As String = entity("table")
        Dim model_name = entity("model_name")

        If model_name = "" Then
            model_name = Utils.nameCamelCase(table_name)
        End If
        If table_name = "" OrElse model_name = "" Then Throw New ApplicationException("No table name or no model name")
        'If _models.Contains(model_name) Then Throw New ApplicationException("Such model already exists")

        'copy DemoDicts.vb to model_name.vb
        Dim path = fw.config("site_root") & "\App_Code\models"
        Dim mdemo = FW.get_file_content(path & "\DemoDicts.vb")
        If mdemo = "" Then Throw New ApplicationException("Can't open DemoDicts.vb")

        'replace: DemoDicts => ModelName, demo_dicts => table_name
        mdemo = mdemo.Replace("DemoDicts", model_name)
        mdemo = mdemo.Replace("demo_dicts", table_name)
        mdemo = mdemo.Replace("db_config = """"", "db_config = """ & entity("db_config") & """")

        'generate code for the model's constructor:
        'set field_*
        Dim codegen = ""
        If entity.ContainsKey("fields") Then
            Dim fields = array2hashtable(entity("fields"), "name")

            'detect id and iname fields
            Dim i = 1
            Dim fld_int As Hashtable = Nothing
            Dim fld_identity As Hashtable = Nothing
            Dim fld_iname As Hashtable = Nothing
            Dim is_normalize_names = False
            For Each fld As Hashtable In entity("fields")
                'find identity
                If fld_identity Is Nothing AndAlso fld("is_identity") = "1" Then
                    fld_identity = fld
                End If

                'first int field
                If fld_int Is Nothing AndAlso fld("fw_type") = "int" Then
                    fld_int = fld
                End If

                'for iname - just use 2nd to 4th field which not end with ID, varchar type and has some maxlen
                If fld_iname Is Nothing AndAlso i >= 2 AndAlso i <= 4 AndAlso fld("fw_type") = "varchar" AndAlso Utils.f2int(fld("maxlen")) > 0 AndAlso Right(fld("name"), 2).ToLower() <> "id" Then
                    fld_iname = fld
                End If

                If Regex.IsMatch(fld("name"), "^[\w_]", RegexOptions.IgnoreCase) Then
                    'normalize names only if at least one field contains non-alphanumeric chars
                    is_normalize_names = True
                End If

                i += 1
            Next

            If fld_identity Is Nothing AndAlso fld_int IsNot Nothing AndAlso fields.Count = 2 Then
                'this is looks like lookup table (id/name fields only) without identity - just set id field as first int field
                fld_identity = fld_int
            End If

            If fld_iname Is Nothing AndAlso fld_identity IsNot Nothing Then
                'if no iname field found - just use ID field
                fld_iname = fld_identity
            End If

            If fld_identity IsNot Nothing AndAlso fld_identity("name") <> "id" Then
                codegen &= "        field_id = """ & fld_identity("name") & """" & vbCrLf
            End If
            If fld_iname IsNot Nothing AndAlso fld_iname("name") <> "iname" Then
                codegen &= "        field_iname = """ & fld_iname("name") & """" & vbCrLf
            End If

            'also reset fw fields if such not exists
            If Not fields.ContainsKey("status") Then
                codegen &= "        field_status = """"" & vbCrLf
            End If
            If Not fields.ContainsKey("add_users_id") Then
                codegen &= "        field_add_users_id = """"" & vbCrLf
            End If
            If Not fields.ContainsKey("upd_users_id") Then
                codegen &= "        field_upd_users_id = """"" & vbCrLf
            End If
            If Not fields.ContainsKey("upd_time") Then
                codegen &= "        field_upd_time = """"" & vbCrLf
            End If

            If Not Utils.f2bool(entity("is_fw")) Then
                codegen &= "        is_normalize_names = True" & vbCrLf
            End If

            If is_normalize_names Then
                codegen &= "        is_normalize_names = True" & vbCrLf
            End If

        End If


        mdemo = mdemo.Replace("'###CODEGEN", codegen)

        FW.set_file_content(path & "\" & model_name & ".vb", mdemo)
    End Sub

    Private Sub createLookup(entity As Hashtable)
        Dim ltable = fw.model(Of LookupManagerTables).oneByTname(entity("table"))

        Dim columns = ""
        Dim column_names = ""
        Dim fields = Me.array2hashtable(entity("fields"), "fw_name")
        If fields.ContainsKey("icode") Then
            columns &= IIf(columns > "", ",", "") & "icode"
            column_names &= IIf(column_names > "", ",", "") & fields("icode")("iname")
        End If
        If fields.ContainsKey("iname") Then
            columns &= IIf(columns > "", ",", "") & "iname"
            column_names &= IIf(column_names > "", ",", "") & fields("iname")("iname")
        End If
        If fields.ContainsKey("idesc") Then
            columns &= IIf(columns > "", ",", "") & "idesc"
            column_names &= IIf(column_names > "", ",", "") & fields("idesc")("iname")
        End If

        Dim item As New Hashtable From {
                {"tname", entity("table")},
                {"iname", entity("iname")},
                {"columns", columns},
                {"column_names", column_names}
            }
        If ltable.Count > 0 Then
            'replace
            fw.model(Of LookupManagerTables).update(ltable("id"), item)
        Else
            fw.model(Of LookupManagerTables).add(item)
        End If
    End Sub

    Private Sub createController(entity As Hashtable, entities As ArrayList)
        Dim model_name = entity("model_name")
        Dim controller_url = entity("controller_url")
        Dim controller_title = entity("controller_title")

        If controller_url = "" Then controller_url = "/Admin/" & model_name
        Dim controller_name = Replace(controller_url, "/", "")
        If controller_title = "" Then controller_title = Utils.name2human(model_name)

        If model_name = "" Then Throw New ApplicationException("No model or no controller name or no title")
        'If _controllers.Contains(controller_name & "Controller") Then Throw New ApplicationException("Such controller already exists")

        'save back to entity as it can be used by caller
        entity("controller_url") = controller_url
        entity("controller_title") = controller_title

        If Utils.f2bool(entity("controller_is_lookup")) Then
            'if requested controller as a lookup table - just add/update lookup tables, no actual controller creation
            Me.createLookup(entity)
            Return
        End If

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
        updateControllerConfigJson(entity, tpl_to, entities)

        'add controller to sidebar menu
        updateMenuItem(controller_url, controller_title)
    End Sub

    Public Sub updateControllerConfigJson(entity As Hashtable, tpl_to As String, entities As ArrayList)
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
        Dim config = loadJson(Of Hashtable)(config_file)

        updateControllerConfig(entity, config, entities)

        'Utils.jsonEncode(config) - can't use as it produces unformatted json string
        saveJson(config, config_file)
    End Sub

    Public Sub updateControllerConfig(entity As Hashtable, config As Hashtable, entities As ArrayList)
        Dim model_name As String = entity("model_name")
        Dim table_name = entity("table")
        logger("updating config for controller=", entity("controller_url"))

        Dim tables As New Hashtable 'hindex by table name to entities
        Dim fields As ArrayList = entity("fields")
        If fields Is Nothing Then
            'TODO deprecate reading from db, always use entity info
            Dim db As DB
            If entity("db_config") > "" Then
                db = New DB(fw, fw.config("db")(entity("db_config")), entity("db_config"))
            Else
                db = New DB(fw)
            End If
            fields = db.load_table_schema_full(table_name)
            If Not entity.ContainsKey("is_fw") Then entity("is_fw") = True 'TODO actually detect if there any fields to be normalized
            Dim atables = db.tables()
            For Each tbl As String In atables
                tables(tbl) = New Hashtable
            Next
        Else
            For Each tentity As Hashtable In entities
                tables(tentity("table")) = tentity
            Next
        End If

        Dim is_fw = Utils.f2bool(entity("is_fw"))
        Dim hfields As New Hashtable
        Dim sys_fields = Utils.qh("id status add_time add_users_id upd_time upd_users_id")

        Dim saveFields As New ArrayList
        Dim saveFieldsNullable As New ArrayList
        Dim hFieldsMap As New Hashtable   'name => iname
        Dim hFieldsMapFW As New Hashtable 'fw_name => name
        Dim showFieldsLeft As New ArrayList
        Dim showFieldsRight As New ArrayList
        Dim showFormFieldsLeft As New ArrayList
        Dim showFormFieldsRight As New ArrayList 'system fields - to the right

        For Each fld In fields
            logger("field name=", fld("name"), fld)

            If fld("fw_name") = "" Then fld("fw_name") = Utils.name2fw(fld("name")) 'system name using fw standards
            If fld("iname") = "" Then fld("iname") = Utils.name2human(fld("name")) 'human name using fw standards

            hfields(fld("name")) = fld
            hFieldsMap(fld("name")) = fld("iname")
            If Not is_fw Then
                hFieldsMap(fld("fw_name")) = fld("iname")
                hFieldsMapFW(fld("fw_name")) = fld("name")
            End If

            Dim sf As New Hashtable  'show fields
            Dim sff As New Hashtable 'showform fields
            Dim is_skip = False
            sf("field") = fld("name")
            sf("label") = fld("iname")
            sf("type") = "plaintext"

            sff("field") = fld("name")
            sff("label") = fld("iname")

            If fld("is_nullable") = "0" AndAlso fld("default") Is Nothing Then
                sff("required") = True 'if not nullable and no default - required
            End If

            If fld("is_nullable") = "1" Then
                saveFieldsNullable.Add(fld("name"))
            End If

            Dim maxlen = Utils.f2int(fld("maxlen"))
            If maxlen > 0 Then sff("maxlength") = maxlen
            If fld("fw_type") = "varchar" Then
                If maxlen <= 0 OrElse fld("name") = "idesc" Then 'large text if no maxlen or standard idesc
                    sf("type") = "markdown"
                    sff("type") = "textarea"
                    sff("rows") = 5
                    sff("class_control") = "markdown autoresize" 'or fw-html-editor or fw-html-editor-short
                Else
                    'normal text input
                    sff("type") = "input"
                    If maxlen < 255 Then
                        Dim col As Integer = Math.Round(maxlen / 255 * 9 * 4)
                        If col < 2 Then col = 2 'minimum - 2
                        If col > 9 Then col = 9
                        sff("class_contents") = "col-md-" & col
                    End If

                End If
            ElseIf fld("fw_type") = "int" Then
                'int fields could be: foreign keys, yes/no, just a number input

                'check foreign keys - and make type=select
                Dim is_fk = False
                If entity.ContainsKey("foreign_keys") Then
                    For Each fkinfo As Hashtable In entity("foreign_keys")
                        If fkinfo("column") = fld("name") Then
                            is_fk = True
                            Dim mname = _tablename2model(Utils.name2fw(fkinfo("pk_table")))

                            sf("lookup_model") = mname
                            'sf("lookup_field") = "iname"    
                            sf("type") = "plaintext_link"

                            sff("type") = "select"
                            sff("lookup_model") = mname
                            If Regex.Replace(fld("default") & "", "\D+", "") = "0" Then 'remove all non-digits
                                'if default is 0 - allow 0 option
                                sff("is_option0") = True
                            Else
                                sff("is_option_empty") = True
                            End If
                            sff("option0_title") = "- select -"

                            sff("class_contents") = "col-md-3"
                            Exit For
                        End If
                    Next
                End If

                If Not is_fk Then
                    If fld("name") = "parent_id" Then
                        'special case - parent_id
                        Dim mname = model_name

                        sf("lookup_model") = mname
                        'sf("lookup_field") = "iname"
                        sf("type") = "plaintext_link"

                        sff("type") = "select"
                        sff("lookup_model") = mname
                        sff("is_option0") = True
                        sff("class_contents") = "col-md-3"
                    ElseIf fld("fw_subtype") = "boolean" Then 'not sure about tinyint and unsignedtinyint
                        'make it as yes/no radio
                        sff("type") = "yesno"
                        sff("is_inline") = True
                        sff("class_contents") = "d-flex align-items-center"
                    Else
                        sff("type") = "number"
                        sff("min") = 0
                        sff("max") = 999999
                        sff("class_contents") = "col-md-3"
                    End If
                End If

            ElseIf fld("fw_type") = "float" Then
                sff("type") = "number"
                sff("step") = 0.1
                sff("class_contents") = "col-md-3"
            ElseIf fld("fw_type") = "datetime" Then
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
                sff.Remove("class_contents")
                sff.Remove("required")
            End If

            'special fields
            Select Case fld("name")
                Case "iname"
                    sff("validate") = "exists" 'unique field
                Case "att_id" 'Single attachment field - TODO better detect on foreign key to "att" table
                    sf("type") = "att"
                    sf("label") = "Attachment"
                    sf("class_contents") = "col-md-3"
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
                    sff.Remove("class_contents")
                Case "upd_time"
                    sf("label") = "Updated on"
                    sf("type") = "updated"

                    sff("label") = "Updated on"
                    sff("type") = "updated"
                    sff.Remove("class_contents")
                Case "add_users_id", "upd_users_id"
                    is_skip = True
                Case Else
                    If Regex.IsMatch(fld("iname"), "\bState$") Then
                        'if human name ends with State - make it State select
                        sf("lookup_tpl") = "/common/sel/state.sel"

                        sff("type") = "select"
                        sff("lookup_tpl") = "/common/sel/state.sel"
                        sff("is_option_empty") = True
                        sff("option0_title") = "- select -"
                        sff("class_contents") = "col-md-3"
                    Else
                        'nothing else
                    End If
            End Select

            If is_skip Then Continue For

            Dim is_sys = False
            If fld("is_identity") = "1" _
                OrElse sys_fields.Contains(fld("name")) _
                OrElse sf("type") = "att" _
                OrElse sf("type") = "att_links" _
            Then
                'add to system fields
                showFieldsRight.Add(sf)
                showFormFieldsRight.Add(sff)
                is_sys = True
            Else
                showFieldsLeft.Add(sf)
                showFormFieldsLeft.Add(sff)
            End If

            If Not is_sys OrElse fld("name") = "status" Then
                'add to save fields only if not system (except status)
                saveFields.Add(fld("name"))
            End If
        Next

        'special case - "Lookup via Link Table" - could be multiple tables
        Dim rx_table_link = "^" & Regex.Escape(table_name) & "_(.+?)_link$"
        Dim table_name_linked = ""
        Dim table_name_link = ""
        For Each table In tables.Keys
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
                        {"table_link_id_name", table_name & "_id"},
                        {"table_link_linked_id_name", table_name_linked & "_id"}
                    }
                    Dim sfflink As New Hashtable From {
                        {"field", table_name_linked & "_link"},
                        {"label", "Linked " & table_name_linked},
                        {"type", "multicb"},
                        {"lookup_model", _tablename2model(table_name_linked)},
                        {"table_link", table_name_link},
                        {"table_link_id_name", table_name & "_id"},
                        {"table_link_linked_id_name", table_name_linked & "_id"}
                    }

                    showFieldsLeft.Add(sflink)
                    showFormFieldsLeft.Add(sfflink)
                End If

            End If
        Next
        'end special case for link table

        config("model") = model_name
        config("is_dynamic_index") = True
        config("save_fields_nullable") = saveFieldsNullable
        config("save_fields") = saveFields 'save all non-system
        config("save_fields_checkboxes") = ""
        config("search_fields") = "id" & If(hfields.ContainsKey("iname"), " iname", "") 'id iname

        'either deault sort by iname or id
        config("list_sortdef") = "id desc"
        If hfields.ContainsKey("iname") Then
            config("list_sortdef") = "iname asc"
        Else
            'just get first field
            If fields.Count > 0 Then
                config("list_sortdef") = fields(0)("fw_name")
            End If
        End If

        config.Remove("list_sortmap") 'N/A in dynamic controller
        config.Remove("required_fields") 'not necessary in dynamic controller as controlled by showform_fields required attribute
        config("related_field_name") = "" 'TODO?
        config("list_view") = table_name


        'default fields for list view
        'alternatively - just show couple fields
        'If is_fw Then config("view_list_defaults") = "id" & If(hfields.ContainsKey("iname"), " iname", "") & If(hfields.ContainsKey("add_time"), " add_time", "") & If(hfields.ContainsKey("status"), " status", "")

        'just show all fields, except identity, large text and system fields
        config("view_list_defaults") = ""
        For i = 0 To fields.Count - 1
            If fields(i)("is_identity") = "1" Then Continue For
            If fields(i)("fw_type") = "varchar" AndAlso Utils.f2int(fields(i)("maxlen")) <= 0 Then Continue For
            If is_fw Then
                If fields(i)("name") = "add_time" OrElse fields(i)("name") = "add_users_id" OrElse fields(i)("name") = "upd_time" OrElse fields(i)("name") = "upd_users_id" Then Continue For
                config("view_list_defaults") &= IIf(i = 0, "", " ") & fields(i)("name")
            Else
                config("view_list_defaults") &= IIf(i = 0, "", " ") & fields(i)("fw_name")
            End If
        Next

        If Not is_fw Then
            'nor non-fw tables - just show first 3 fields
            'config("view_list_defaults") = ""
            'For i = 0 To Math.Min(2, fields.Count - 1)
            '    config("view_list_defaults") &= IIf(i = 0, "", " ") & fields(i)("fw_name")
            'Next

            'for non-fw - list_sortmap separately
            config("list_sortmap") = hFieldsMapFW
        End If
        config("view_list_map") = hFieldsMap 'fields to names
        config("view_list_custom") = "status"

        config("is_dynamic_show") = IIf(entity.ContainsKey("controller_is_dynamic_show"), entity("controller_is_dynamic_show"), True)
        If config("is_dynamic_show") Then
            Dim showFields = New ArrayList
            showFields.Add(Utils.qh("type|row"))
            showFields.Add(Utils.qh("type|col class|col-lg-6"))
            showFields.AddRange(showFieldsLeft)
            showFields.Add(Utils.qh("type|col_end"))
            showFields.Add(Utils.qh("type|col class|col-lg-6"))
            showFields.AddRange(showFieldsRight)
            showFields.Add(Utils.qh("type|col_end"))
            showFields.Add(Utils.qh("type|row_end"))
            config("show_fields") = showFields
        End If
        config("is_dynamic_showform") = IIf(entity.ContainsKey("controller_is_dynamic_showform"), entity("controller_is_dynamic_showform"), True)
        If config("is_dynamic_showform") Then
            Dim showFormFields = New ArrayList
            showFormFields.Add(Utils.qh("type|row"))
            showFormFields.Add(Utils.qh("type|col class|col-lg-6"))
            showFormFields.AddRange(showFormFieldsLeft)
            showFormFields.Add(Utils.qh("type|col_end"))
            showFormFields.Add(Utils.qh("type|col class|col-lg-6"))
            showFormFields.AddRange(showFormFieldsRight)
            showFormFields.Add(Utils.qh("type|col_end"))
            showFormFields.Add(Utils.qh("type|row_end"))
            config("showform_fields") = showFormFields
        End If

        'remove all commented items - name start with "#"
        For Each key In config.Keys.Cast(Of String).ToArray()
            If Left(key, 1) = "#" Then config.Remove(key)
        Next

    End Sub

    'convert db.json entity to SQL CREATE TABLE
    Private Function entity2SQL(entity As Hashtable) As String
        Dim result = "CREATE TABLE " & db.q_ident(entity("table")) & " (" & vbCrLf

        Dim i = 1
        For Each field As Hashtable In entity("fields")
            Dim fsql = ""
            If field("name") = "status" Then fsql &= vbCrLf 'add empty line before system fields starting with "status"

            fsql &= "  " & db.q_ident(field("name")).PadRight(21, " ") & " " & entityfield2dbtype(field)
            If field("is_identity") = 1 Then
                fsql &= " IDENTITY(1, 1) PRIMARY KEY CLUSTERED"
            End If
            fsql &= IIf(field("is_nullable") = 0, " NOT NULL", "")
            fsql &= entityfield2dbdefault(field)
            fsql &= entityfield2dbfk(field, entity)

            result &= fsql & IIf(i < entity("fields").Count, ",", "") & vbCrLf
            i += 1
        Next

        result &= ")"

        Return result
    End Function

    Private Function entityfield2dbtype(entity As Hashtable) As String
        Dim result As String

        Select Case entity("fw_type")
            Case "int"
                If entity("fw_subtype") = "boolean" OrElse entity("fw_subtype") = "bit" Then
                    result = "BIT"
                ElseIf entity("numeric_precision") = 3 Then
                    result = "TINYINT"
                Else
                    result = "INT"
                End If
            Case "float"
                result = "FLOAT"
            Case "datetime"
                If entity("fw_subtype") = "date" Then
                    result = "DATE"
                Else
                    result = "DATETIME2"
                End If
            Case Else '"varchar"
                result = "NVARCHAR"
                If entity("maxlen") > 0 And entity("maxlen") < 256 Then
                    result &= "(" & entity("maxlen") & ")"
                Else
                    result &= "(MAX)"
                End If
        End Select

        Return result
    End Function

    Private Function entityfield2dbdefault(entity As Hashtable) As String
        Dim result = ""
        Dim def As String = entity("default")
        If def IsNot Nothing Then
            result &= " DEFAULT "
            'remove outer parentheses if any
            def = Regex.Replace(def, "^\((.+)\)$", "$1")
            def = Regex.Replace(def, "^\((.+)\)$", "$1") 'and again because of ((0)) but don't touch (getdate())

            If Regex.IsMatch(def, "^\d+$") Then
                'only digits
                result &= "(" & def & ")"

            ElseIf def = "getdate()" OrElse Regex.IsMatch(def, "^\=?now\(\)$", RegexOptions.IgnoreCase) Then
                'access now() => getdate()
                result &= "(getdate())"
            Else
                'any other text - quote
                def = Regex.Replace(def, "^'(.*)'$", "$1") 'remove outer quotes if any

                If entity("fw_type") = "int" Then
                    'if field type int - convert to int
                    result &= "(" & db.qi(def) & ")"
                Else
                    result &= "(" & db.q(def) & ")"
                End If
            End If
        End If

        Return result
    End Function

    'if field is referece to other table - add named foreign key
    'CONSTRAINT FK_entity("table_name")_remotetable FOREIGN KEY REFERENCES remotetable(id)
    Private Function entityfield2dbfk(field As Hashtable, entity As Hashtable) As String
        Dim result = ""

        If Not entity.ContainsKey("foreign_keys") Then Return result

        For Each fk As Hashtable In entity("foreign_keys")
            If fk("column") = field("name") Then
                result = " CONSTRAINT FK_" & entity("fw_name") & "_" & Utils.name2fw(fk("pk_table")) & " FOREIGN KEY REFERENCES " & db.q_ident(fk("pk_table")) & "(" & db.q_ident(fk("pk_column")) & ")"
                Exit For
            End If
        Next

        Return result
    End Function

    'return default fields for the entity
    'id[, icode], iname, idesc, status, add_time, add_users_id, upd_time, upd_users_id
    Private Function defaultFields() As ArrayList
        'New Hashtable From {
        '    {"name", "icode"},
        '    {"fw_name", "icode"},
        '    {"iname", "Code"},
        '    {"is_identity", 0},
        '    {"default", ""},
        '    {"maxlen", 64},
        '    {"numeric_precision", Nothing},
        '    {"is_nullable", 1},
        '    {"fw_type", "varchar"},
        '    {"fw_subtype", "nvarchar"}
        '},

        Return New ArrayList From {
            New Hashtable From {
                {"name", "id"},
                {"fw_name", "id"},
                {"iname", "ID"},
                {"is_identity", 1},
                {"default", Nothing},
                {"maxlen", Nothing},
                {"numeric_precision", 10},
                {"is_nullable", 0},
                {"fw_type", "int"},
                {"fw_subtype", "integer"}
            },
            New Hashtable From {
                {"name", "iname"},
                {"fw_name", "iname"},
                {"iname", "Name"},
                {"is_identity", 0},
                {"default", Nothing},
                {"maxlen", 255},
                {"numeric_precision", Nothing},
                {"is_nullable", 0},
                {"fw_type", "varchar"},
                {"fw_subtype", "nvarchar"}
            },
            New Hashtable From {
                {"name", "idesc"},
                {"fw_name", "idesc"},
                {"iname", "Notes"},
                {"is_identity", 0},
                {"default", Nothing},
                {"maxlen", -1},
                {"numeric_precision", Nothing},
                {"is_nullable", 1},
                {"fw_type", "varchar"},
                {"fw_subtype", "nvarchar"}
            }
        }
    End Function

    Private Function defaultFieldsAfter() As ArrayList
        Return New ArrayList From {
            New Hashtable From {
                {"name", "status"},
                {"fw_name", "status"},
                {"iname", "Status"},
                {"is_identity", 0},
                {"default", 0},
                {"maxlen", Nothing},
                {"numeric_precision", 3},
                {"is_nullable", 0},
                {"fw_type", "int"},
                {"fw_subtype", "tinyint"}
            },
            New Hashtable From {
                {"name", "add_time"},
                {"fw_name", "add_time"},
                {"iname", "Added on"},
                {"is_identity", 0},
                {"default", "getdate()"},
                {"maxlen", Nothing},
                {"numeric_precision", Nothing},
                {"is_nullable", 0},
                {"fw_type", "datetime"},
                {"fw_subtype", "datetime2"}
            },
            New Hashtable From {
                {"name", "add_users_id"},
                {"fw_name", "add_users_id"},
                {"iname", "Added by"},
                {"is_identity", 0},
                {"default", Nothing},
                {"maxlen", Nothing},
                {"numeric_precision", 10},
                {"is_nullable", 1},
                {"fw_type", "int"},
                {"fw_subtype", "int"}
            },
            New Hashtable From {
                {"name", "upd_time"},
                {"fw_name", "upd_time"},
                {"iname", "Updated on"},
                {"is_identity", 0},
                {"default", Nothing},
                {"maxlen", Nothing},
                {"numeric_precision", Nothing},
                {"is_nullable", 1},
                {"fw_type", "datetime"},
                {"fw_subtype", "datetime2"}
            },
            New Hashtable From {
                {"name", "upd_users_id"},
                {"fw_name", "upd_users_id"},
                {"iname", "Updated by"},
                {"is_identity", 0},
                {"default", Nothing},
                {"maxlen", Nothing},
                {"numeric_precision", 10},
                {"is_nullable", 1},
                {"fw_type", "int"},
                {"fw_subtype", "int"}
            }
        }
    End Function

    Private Function addressFields(field_name As String) As ArrayList
        Dim m = Regex.Match(field_name, "(.*?)(Address)$", RegexOptions.IgnoreCase)
        Dim prefix As String = m.Groups(1).Value
        Dim city_name = prefix & "city"
        Dim state_name = prefix & "state"
        Dim zip_name = prefix & "zip"
        Dim country_name = prefix & "country"
        If m.Groups(2).Value = "Address" Then
            city_name = prefix & "City"
            state_name = prefix & "State"
            zip_name = prefix & "Zip"
            country_name = prefix & "Country"
        End If

        Return New ArrayList From {
            New Hashtable From {
                {"name", field_name},
                {"fw_name", Utils.name2fw(field_name)},
                {"iname", Utils.name2human(field_name)},
                {"is_identity", 0},
                {"default", ""},
                {"maxlen", 255},
                {"numeric_precision", Nothing},
                {"is_nullable", 0},
                {"fw_type", "varchar"},
                {"fw_subtype", "nvarchar"}
            },
            New Hashtable From {
                {"name", field_name & "2"},
                {"fw_name", Utils.name2fw(field_name & "2")},
                {"iname", Utils.name2human(field_name & "2")},
                {"is_identity", 0},
                {"default", ""},
                {"maxlen", 255},
                {"numeric_precision", Nothing},
                {"is_nullable", 0},
                {"fw_type", "varchar"},
                {"fw_subtype", "nvarchar"}
            },
            New Hashtable From {
                {"name", city_name},
                {"fw_name", Utils.name2fw(city_name)},
                {"iname", Utils.name2human(city_name)},
                {"is_identity", 0},
                {"default", ""},
                {"maxlen", 64},
                {"numeric_precision", Nothing},
                {"is_nullable", 0},
                {"fw_type", "varchar"},
                {"fw_subtype", "nvarchar"}
            },
            New Hashtable From {
                {"name", state_name},
                {"fw_name", Utils.name2fw(state_name)},
                {"iname", Utils.name2human(state_name)},
                {"is_identity", 0},
                {"default", ""},
                {"maxlen", 2},
                {"numeric_precision", Nothing},
                {"is_nullable", 0},
                {"fw_type", "varchar"},
                {"fw_subtype", "nvarchar"}
            },
            New Hashtable From {
                {"name", zip_name},
                {"fw_name", Utils.name2fw(zip_name)},
                {"iname", Utils.name2human(zip_name)},
                {"is_identity", 0},
                {"default", ""},
                {"maxlen", 11},
                {"numeric_precision", Nothing},
                {"is_nullable", 0},
                {"fw_type", "varchar"},
                {"fw_subtype", "nvarchar"}
            }
        }
    End Function

    'update by url
    Private Sub updateMenuItem(controller_url As String, controller_title As String)
        Dim fields = New Hashtable From {
                {"url", controller_url},
                {"iname", controller_title},
                {"controller", Replace(controller_url, "/", "")}
            }

        Dim mitem = db.row("menu_items", New Hashtable From {{"url", controller_url}})
        If mitem.Count > 0 Then
            db.update("menu_items", fields, New Hashtable From {{"id", mitem("id")}})
        Else
            'add to menu_items
            db.insert("menu_items", fields)
        End If
    End Sub

    Private Function isFwTableName(table_name As String) As Boolean
        Dim tables = Utils.qh(FW_TABLES)
        Return tables.ContainsKey(table_name.ToLower())
    End Function


End Class
