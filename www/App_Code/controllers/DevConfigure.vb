' Configuration check controller for Developers
'  - perform basic testing of configuration
'  WARNING: better to remove this file on production
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018  Oleg Savchuk www.osalabs.com

Public Class DevConfigureController
    Inherits FwController
    Public Shared Shadows access_level As Integer = -1

    Protected model As DemoDicts

    Public Function IndexAction() As Hashtable
        Dim ps As New Hashtable

        ps("hide_sidebar") = True
        ps("config_file_name") = fw.config("config_override")

        ps("is_db_config") = False
        If fw.config("db") IsNot Nothing AndAlso fw.config("db")("main") IsNot Nothing AndAlso fw.config("db")("main")("connection_string") > "" Then ps("is_db_config") = True

        Dim db As DB
        ps("is_db_conn") = False
        ps("is_db_tables") = False
        If ps("is_db_config") Then
            Try
                db = New DB(fw)
                db.connect()
                ps("is_db_conn") = True

                Try
                    Dim value = db.value("select count(*) from menu_items") 'just a last table in database.sql script
                    ps("is_db_tables") = True
                Catch ex As Exception
                    ps("db_tables_err") = ex.Message
                End Try

            Catch ex As Exception
                ps("db_conn_err") = ex.Message
            End Try
        End If

        ps("is_write_dirs") = False
        Dim upload_dir As String = fw.config("site_root") & fw.config("UPLOAD_DIR")
        'check if dir is writable
        ps("is_write_dirs") = isWritable(upload_dir)

        ps("is_write_langok") = True
        If isWritable(fw.config("template") & "/lang") AndAlso Not Utils.f2bool(fw.config("IS_DEV")) Then ps("is_write_langok") = False

        'obsolete in .net 4
        'If System.Security.SecurityManager.IsGranted(writePermission) Then ps("is_write_dirs") = True

        ps("is_error_log") = False
        ps("is_error_log") = isWritable(fw.config("log"))

        ps("error_log_size") = Utils.bytes2str(Utils.fileSize(fw.config("log")))

        Return ps
    End Function

    Private Function isWritable(filepath As String) As Boolean
        Dim writePermission As New System.Security.Permissions.FileIOPermission(System.Security.Permissions.FileIOPermissionAccess.Write, filepath)
        Dim permissionSet As New System.Security.PermissionSet(System.Security.Permissions.PermissionState.None)
        permissionSet.AddPermission(writePermission)
        Return permissionSet.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet)
    End Function

End Class
