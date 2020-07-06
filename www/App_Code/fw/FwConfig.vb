' App Configuration class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.IO
Public Class FwConfig
    Public Shared hostname As String
    Public Shared settings As Hashtable
    Public Shared route_prefixes_rx As String

    Private Shared ReadOnly locker As New Object

    Public Shared Sub init(req As System.Web.HttpRequest, Optional ByVal hostname As String = "")
        'appSettings is Shared, so it's lifetime same as application lifetime 
        'if appSettings already initialized no need to read web.config again
        SyncLock locker
            If settings IsNot Nothing AndAlso settings.Count > 0 AndAlso settings.ContainsKey("_SETTINGS_OK") Then Exit Sub
            FwConfig.hostname = hostname
            initDefaults(req, hostname)
            readSettings()
            specialSettings()

            settings("_SETTINGS_OK") = True 'just a marker to ensure we have all settings set
        End SyncLock
    End Sub

    'reload settings
    Public Shared Sub reload()
        initDefaults(HttpContext.Current.Request, FwConfig.hostname)
        readSettings()
        specialSettings()
    End Sub

    'init default settings
    Private Shared Sub initDefaults(req As System.Web.HttpRequest, Optional ByVal hostname As String = "")
        settings = New Hashtable

        If String.IsNullOrEmpty(hostname) Then hostname = req.ServerVariables("HTTP_HOST")
        settings("hostname") = hostname

        settings("ROOT_URL") = Regex.Replace(req.ApplicationPath, "\/$", "") 'removed last / if any
        settings("site_root") = Regex.Replace(req.PhysicalApplicationPath, "\\$", "") 'removed last \ if any

        settings("template") = settings("site_root") & "\App_Data\template"
        settings("log") = settings("site_root") & "\App_Data\logs\main.log"
        settings("log_max_size") = 100 * 1024 * 1024 '100 MB is max log size
        settings("tmp") = Path.GetTempPath

        Dim http As String = "http://"
        If req.ServerVariables("HTTPS") = "on" Then http = "https://"
        Dim port As String = ":" & req.ServerVariables("SERVER_PORT")
        If port = ":80" OrElse port = ":443" Then port = ""
        settings("ROOT_DOMAIN") = http & req.ServerVariables("SERVER_NAME") & port

    End Sub

    'read setting into appSettings
    Private Shared Sub readSettings()
        Dim appSettings As NameValueCollection = ConfigurationManager.AppSettings()

        Dim keys() As String = appSettings.AllKeys
        For Each key As String In keys
            parseSetting(key, appSettings(key))
        Next
    End Sub
    Private Shared Sub parseSetting(key As String, ByRef value As String)
        Dim delim As String = "|"
        If InStr(key, delim) = 0 Then
            settings(key) = parseSettingValue(value)
        Else
            Dim keys() As String = Split(key, delim)

            'build up all hashtables tree
            Dim ptr As Hashtable = settings
            For i As Integer = 0 To keys.Length - 2
                Dim hkey As String = keys(i)
                If ptr.ContainsKey(hkey) AndAlso TypeOf (ptr) Is Hashtable Then
                    ptr = ptr(hkey) 'going deep into
                Else
                    ptr(hkey) = New Hashtable 'this will overwrite any value, i.e. settings names must be different on same level
                    ptr = ptr(hkey)
                End If
            Next
            'assign value to key element in deepest hashtree
            ptr(keys(keys.Length - 1)) = parseSettingValue(value)
        End If
    End Sub
    'parse value to type, supported:
    'boolean
    'int
    'qh - using Utils.qh()
    Private Shared Function parseSettingValue(ByRef value As String) As Object
        Dim result As Object
        Dim m As Match = Regex.Match(value, "^~(.*?)~")
        If m.Success Then 'if value contains type = "~int~25" - then cast value to the type
            Dim value2 As String = Regex.Replace(value, "^~.*?~", "")
            Select Case m.Groups(1).Value
                Case "int"
                    Dim ival As Integer
                    If Not Integer.TryParse(value2, ival) Then ival = 0
                    result = ival
                Case "boolean"
                    Dim ibool As Boolean
                    If Not Boolean.TryParse(value2, ibool) Then ibool = False
                    result = ibool
                Case "qh"
                    result = Utils.qh(value2)
                Case Else
                    result = value2
            End Select
        Else
            result = String.Copy(value)
        End If

        Return result
    End Function

    'set special settings after we read config
    Private Shared Sub specialSettings()
        Dim hostname As String = settings("hostname")

        Dim overs As Hashtable = settings("override")
        For Each over_name As String In overs.Keys
            If Regex.IsMatch(hostname, overs(over_name)("hostname_match")) Then
                settings("config_override") = over_name
                Utils.mergeHashDeep(settings, overs(over_name))
                Exit For
            End If
        Next

        'convert strings to specific types
        Dim log_level As LogLevel = LogLevel.INFO 'default log level if No or Wrong level in config
        If settings.ContainsKey("log_level") Then
            [Enum].TryParse(Of LogLevel)(settings("log_level"), True, log_level)
        End If
        settings("log_level") = log_level

        'default settings that depend on other settings
        If Not settings.ContainsKey("ASSETS_URL") Then
            settings("ASSETS_URL") = settings("ROOT_URL") & "/assets"
        End If

    End Sub


    'prefixes used so Dispatcher will know that url starts not with a full controller name, but with a prefix, need to be added to controller name
    'return regexp str that cut the prefix from the url, second capturing group captures rest of url after the prefix
    Public Shared Function getRoutePrefixesRX() As String
        If String.IsNullOrEmpty(route_prefixes_rx) Then
            'prepare regexp - escape all prefixes
            Dim r As New ArrayList()
            For Each url As String In settings("route_prefixes").Keys
                r.Add(Regex.Escape(url))
            Next

            route_prefixes_rx = "^(" & String.Join("|", CType(r.ToArray(GetType(String)), String())) & ")(/.*)?$"
        End If

        Return route_prefixes_rx
    End Function

End Class
