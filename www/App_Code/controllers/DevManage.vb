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
        If model_name = "" OrElse controller_url = "" Then Throw New ApplicationException("No model or no controller name")
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

        'TODO
        'replace in templates: DemoDynamic to Title
        'replace in url.html /Admin/DemosDynamic to controller_url
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

        fw.FLASH("success", controller_name & ".vb controller created, " & controller_url & ", templates copied")
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

End Class
