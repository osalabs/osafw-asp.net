' Backup controller
' /Sys/Backup - called from TaskScheduler(Cron) - perform backup tasks (db + site). IMPORTANT: add /App_Code/cron_backup.vbs to Windows Task Scheduler for automatic backups
' /Sys/Backup/(Download)[?date=YYYY-MM-DD] - download backup (if no date defined - return latest)
'
' optional - to backup site files to single zip - install Ionic.Zip and set is_backup_zip = true
' optional - to save backups to S3 - install AWSSDK and set is_backup_to_S3 = true
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2016 Oleg Savchuk www.osalabs.com

#Const is_backup_zip = False   'backup site files to single zip file. Requires Ionic.Zip
#Const is_backup_to_S3 = False 'set to True to save backups to S3, also set all other aws_* parameters. Requires AWSSDK.Core and AWSSDK.S3

Imports System.IO

#If is_backup_zip Then
Imports Ionic.Zip
#End If
#If is_backup_to_S3 Then
Imports Amazon
#End If

Public Class SysBackupController
    Inherits FwController
    'configuration - TBD - move to web.config
    Public backup_dir As String = "c:\_backup" 'all full backups stored here for "keep_days" days
    Public db_backup_dir As String = "c:\_backup\db" 'all db backups stored here for "keep_days" days
    Public files_source_dir As String '= "c:\tmp\test\www2" 'if not defined - site_root from config will be used
    Public keep_days As Integer = 7 'days to keep backups, i.e. all older backups will be cleaned up
    Public keep_days_S3 As Integer = 31 'days to keep backups in S3, i.e. all older backups will be cleaned up (automatically by S3 expiration rules)
    Public aws_key As String = "" 'if empty - no backup to S3 performed
    Public aws_secret As String = "YYY"
    Public aws_region As String = "us-west-2" 'us-west-2=Oregon
    Public aws_bucket As String = "xxxxxxx-backups" 'bucket name where to store backups, autocreated if not exists (should be globally unique on S3)
    Public download_token As String = "FWB@CKUP" 'password to use in download links (to automate downloads from external place)

    'working vars
    Private ReadOnly aexclude_ext As String = ".log .svn .git"
    Private exclude_ext As Hashtable
    Private db_name As String
    Private timestamp As String
    Private db_backup_filepath As String
    Private full_backup_filepath As String
    Private aws_client As Object 'S3.AmazonS3Client

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        'set defaults
        If Not files_source_dir > "" Then files_source_dir = fw.config("site_root")
        exclude_ext = Utils.qh(aexclude_ext)
    End Sub

    'index just show form with POST
    Public Function IndexAction() As Hashtable
        If Utils.f2int(fw.SESSION("access_level")) < Users.ACL_SITEADMIN Then Throw New AuthException("Access Denied")

        Dim ps As New Hashtable
        Dim outfile = get_latest_backup_file()
        If outfile > "" Then
            ps("latest_backup") = Utils.fileName(outfile)
            ps("latest_backup_size") = Utils.bytes2str(Utils.fileSize(outfile))
        Else
            ps("latest_backup") = "none"
        End If

        ps("backup_dir") = backup_dir
        ps("keep_days") = keep_days
        ps("keep_days_S3") = keep_days_S3
        Return ps
    End Function

    'perform backup tasks - only if called from localhost
    Public Sub SaveAction()
        'only allow to be called from local host OR if user logged as an admin
        If Not is_local_request() AndAlso Utils.f2int(fw.SESSION("access_level")) < Users.ACL_SITEADMIN Then
            logger(fw.req.ServerVariables)
            Throw New ApplicationException("Wrong Request")
        End If

        'prepare
        'backup tasks might take long time - set script timeout to 1 hour = 3600 seconds
        HttpContext.Current.Server.ScriptTimeout = 3600
        get_timestamp()
        get_db_name()

        '- backup db to file
        rw("DB backup...")
        do_backup_db()

        '- backup site to zip
        rw("Site backup...")
        do_backup_files()

        '- cleanup >7 days
        rw("Cleanup local old backups (>" & keep_days & " days)...")
        do_cleanup()

        Try
            '- also save to S3
            rw("Store to S3...")
            store_to_s3()

        Catch ex As Exception
            rw("Warning: Can't store to S3")
            rw(ex.Message)
            logger("WARN: Can't store to S3")
            logger(ex.Message)
        End Try

        rw("Backup Completed")
        logger("Backup Completed")
    End Sub

    Public Sub DownloadAction()
        Dim sdt As String = reqs("date") 'format yyyy-MM-dd.hhmmss

        'check that we logged as an admin (alternatively - check some token passed in URL, but in this case https is required - TODO)
        If Utils.f2int(fw.SESSION("access_level")) < Users.ACL_SITEADMIN AndAlso download_token > "" AndAlso reqs("token") <> download_token Then Throw New AuthException("Access Denied")

        Dim outfile As String
        If sdt > "" Then
            'get particular backup
            sdt = Regex.Replace(sdt, "[/\\]+", "") 'armor +1
            get_db_name()
            outfile = backup_dir & "\" & db_name & "_full_" & sdt & ".zip"
            logger(outfile)
        Else
            'find latest backup
            outfile = get_latest_backup_file()
        End If

        If String.IsNullOrEmpty(outfile) OrElse Not File.Exists(outfile) Then Throw New ApplicationException("No backups found")

        fw.file_response(outfile, Path.GetFileName(outfile))
    End Sub

    'TODO - make SysBackup model and move there?
    'request is local if remote address is localhost OR remote address same as server's address
    Private Function is_local_request() As Boolean
        Dim ipHostInfo As Net.IPHostEntry = Net.Dns.GetHostEntry(fw.req.ServerVariables("SERVER_NAME")) 'get the server IP. Net.Dns.GetHostName() returns windows host name
        Return fw.req.ServerVariables("REMOTE_ADDR") = "::1" OrElse fw.req.ServerVariables("REMOTE_ADDR") = "127.0.0.1" OrElse fw.req.ServerVariables("REMOTE_ADDR") = ipHostInfo.AddressList(0).ToString()
    End Function

    Private Function get_timestamp() As String
        timestamp = Now().ToString("yyyy-MM-dd.HHmmss")
        Return timestamp
    End Function

    Private Function get_db_name() As String
        db_name = db.value("SELECT DB_NAME()")
        Return db_name
    End Function

    Private Sub do_backup_db()
        db_backup_filepath = db_backup_dir & "\" & db_name & timestamp & ".bak"

        Dim sql As String = "BACKUP DATABASE [" & db_name & "] TO DISK =" & db.q(db_backup_filepath)
        db.exec(sql)
    End Sub

    Private Sub do_backup_files()

        full_backup_filepath = backup_dir & "\" & db_name & "_full_" & timestamp & ".zip"
        logger("backing up [" & files_source_dir & "] to [" & full_backup_filepath & "]")

#If is_backup_zip Then
        Using zip As New ZipFile
            'optionally set compression
            'zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression
            'zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestSpeed

            Dim zip_subdir As String
            Dim files = Directory.GetFiles(files_source_dir, "*", SearchOption.AllDirectories)
            For Each file As String In files
                zip_subdir = Path.GetDirectoryName(file).Replace(files_source_dir, "")

                'skip excludes by ext TODO - not only by ext
                'and skip .svn dir
                Dim ext = Path.GetExtension(file).ToLower
                If exclude_ext.ContainsKey(ext) Or Regex.IsMatch(zip_subdir, "^\\\.svn", RegexOptions.IgnoreCase) Then Continue For

                zip.AddFile(file, zip_subdir) 'add file to zip but to subdirectory from the root directory name 
            Next

            'also include db_backup_filepath to the root of zip
            If File.Exists(db_backup_filepath) Then
                zip.AddFile(db_backup_filepath, "")
            End If

            zip.Save(full_backup_filepath)
        End Using
#Else
        rw("No backup files to zip configured")
        logger("No backup files to zip configured")
#End If
    End Sub

    Private Sub do_cleanup()
        'cleanup db first
        Dim files = Directory.GetFiles(db_backup_dir, "*.bak")
        For Each f As String In files
            If DateDiff(DateInterval.Day, File.GetCreationTime(f), Now()) > keep_days Then
                File.Delete(f)
            End If
        Next

        'cleanup full backups
        files = Directory.GetFiles(backup_dir, "*.zip")
        For Each f As String In files
            If DateDiff(DateInterval.Day, File.GetCreationTime(f), Now()) > keep_days Then
                File.Delete(f)
            End If
        Next

    End Sub

    'store file full_backup_filepath to S3
    Private Sub store_to_s3()
#If Not is_backup_to_S3 Then
        aws_key = ""
#End If
        If String.IsNullOrEmpty(aws_key) Then
            rw("No S3 backup configured")
            logger("No S3 backup configured")
            Return
        End If

#If is_backup_to_S3 Then
        Dim prefix As String = db_name & "/" 'store to bucket under /db_name/backupfilename.zip so we can store backups for multiple dbs in one bucket

        Amazon.AWSConfigs.AWSRegion = aws_region
        Using aws_client = New S3.AmazonS3Client(aws_key, aws_secret)
            'create bucket if not exists
            If Not S3.Util.AmazonS3Util.DoesS3BucketExist(aws_client, aws_bucket) Then aws_client.PutBucket(aws_bucket)

            'check and if necessary define backups life according to keep_days_S3
            Dim lifeCycleConfiguration As S3.Model.LifecycleConfiguration = aws_client.GetLifecycleConfiguration(aws_bucket).Configuration
            Dim is_expiration_rule_exists As Boolean = False
            If lifeCycleConfiguration IsNot Nothing Then
                For Each rule In lifeCycleConfiguration.Rules
                    If rule.Id = "autoexpire" Then
                        is_expiration_rule_exists = True
                    End If
                Next
            End If
            If Not is_expiration_rule_exists Then
                lifeCycleConfiguration.Rules.Add(New S3.Model.LifecycleRule With {
                                                .Id = "autoexpire",
                                                .Prefix = prefix,
                                                .Status = S3.LifecycleRuleStatus.Enabled,
                                                .Expiration = New S3.Model.LifecycleRuleExpiration With {.Days = keep_days_S3}
                                                })
                aws_client.PutLifecycleConfiguration(aws_bucket, lifeCycleConfiguration)
            End If

            'store backup recent file
            Dim req = New S3.Model.PutObjectRequest()
            req.BucketName = aws_bucket
            req.Key = prefix & Path.GetFileName(full_backup_filepath)
            req.FilePath = full_backup_filepath
            logger("backing up to S3 [" & req.Key & "]")
            aws_client.PutObject(req)
        End Using
#End If

    End Sub

    Private Function get_latest_backup_file() As String
        Dim result = ""

        Dim files = Directory.GetFiles(backup_dir, "*.zip")
        Dim lasttime As Date = Date.MinValue
        For Each f As String In files
            If File.GetCreationTime(f) > lasttime Then result = f
        Next
        Return result
    End Function

End Class

