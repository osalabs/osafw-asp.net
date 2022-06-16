' S3 Storage model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

'https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/s3-apis-intro.html
'https://docs.aws.amazon.com/AmazonS3/latest/dev/UploadObjSingleOpNET.html
'https://docs.aws.amazon.com/AmazonS3/latest/dev/HLuploadFileDotNet.html

'this module will work if user for defined AWSAccessKey will have permissions like:
' YOURBUCKETNAME is same as defined in S3Bucket
' you could optionally add /S3Root/* after YOURBUCKETNAME to limit access only to specific root prefix
'
'{
'    "Version": "2012-10-17",
'    "Statement": [
'        {
'            "Sid": "XXXXXXXXXXXXXXXXXX",
'            "Effect": "Allow",
'            "Action": [
'                "s3:*"
'            ],
'            "Resource": [
'                "arn:aws:s3:::YOURBUCKETNAME/*"  <-- note /* here
'            ]
'        }
'    ]
'}

#Const is_S3 = False 'if you use Amazon.S3 set to True here and in Att model

#If is_S3 Then

Imports Amazon.S3
Imports Amazon.S3.Model

Public Class S3
    Inherits FwModel

    Public region As String = ""
    Public bucket As String = ""
    Public root As String = ""

    Public client As AmazonS3Client
    'params defined in web.config:
    ' fw.config("AWSAccessKey") - access key
    ' fw.config("AWSSecretKey") - secret key
    ' fw.config("AWSRegion") - region "us-west-2"
    ' fw.config("S3Bucket") - bucket name "xyz"
    ' fw.config("S3Root") - root folder under bucket, default ""

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        table_name = "xxx"

        initClient() 'automatically init client on start
    End Sub

    'initialize client
    'root should end with "/" if non-empty
    Public Function initClient(Optional access_key As String = "", Optional secret_key As String = "", Optional region As String = "", Optional bucket As String = "", Optional root As String = "") As AmazonS3Client
        Dim akey As String = IIf(access_key > "", access_key, fw.config("AWSAccessKey"))
        Dim skey As String = IIf(secret_key > "", secret_key, fw.config("AWSSecretKey"))
        'region is defined in web.config "AWSRegion"

        Me.region = IIf(region > "", region, fw.config("AWSRegion"))
        Me.bucket = IIf(bucket > "", bucket, fw.config("S3Bucket"))
        Me.root = IIf(root > "", root, fw.config("S3Root"))

        client = New AmazonS3Client(akey, skey, Amazon.RegionEndpoint.GetBySystemName(Me.region)) ', Amazon.RegionEndpoint.USWest2

        Return client
    End Function

    'foldername - relative to the S3Root
    'foldername should not contain / at the begin or end
    Public Function createFolder(foldername As String) As PutObjectResponse
        foldername = Regex.Replace(foldername, "^\/|\/$", "") 'remove / from begin and end

        'create /att folder
        Dim request As New PutObjectRequest With {
            .BucketName = Me.bucket,
            .Key = Me.root & foldername & "/",
            .ContentBody = String.Empty
            }
        'logger(client.Config.RegionEndpointServiceName)
        'logger(request.BucketName)
        'logger(request.Key)
        Dim response = client.PutObject(request)
        'logger("folder created: " & request.Key)
        'logger("response:" & response.HttpStatusCode)
        'logger("response:" & response.ToString())
        Return response
    End Function

    Public Function uploadFilepath(key As String, filepath As String, Optional disposition As String = "") As PutObjectResponse
        logger("uploading to S3: key=[" & key & "], filepath=[" & filepath & "]")
        Dim request = New PutObjectRequest With {
                .BucketName = Me.bucket,
                .Key = Me.root & key,
                .FilePath = filepath
                }
        request.Headers("Content-Type") = fw.model(Of Att).getMimeForExt(UploadUtils.getUploadFileExt(filepath))

        If disposition > "" Then
            Dim filename = Replace(System.IO.Path.GetFileName(filepath), """", "'") 'replace quotes
            request.Headers("Content-Disposition") = "inline; filename=""" & filename & """"
        End If

        Dim response = client.PutObject(request)
        'logger("uploaded to: " & request.Key)
        'logger("HttpStatusCode=" & response.HttpStatusCode)
        Return response
    End Function

    ''' <summary>
    '''  upload HttpPostedFile to the S3
    ''' </summary>
    ''' <param name="key">relative to the S3Root</param>
    ''' <param name="file">file from http upload</param>
    ''' <param name="disposition">if defined - Content-Disposition with file.FileName added</param>
    ''' <returns></returns>
    ''' alternative way for disposition - in get https://docs.aws.amazon.com/AmazonS3/latest/dev/RetrievingObjectUsingNetSDK.html
    Public Function uploadPostedFile(key As String, file As HttpPostedFile, Optional disposition As String = "") As PutObjectResponse
        Dim request = New PutObjectRequest With {
                .BucketName = Me.bucket,
                .Key = Me.root & key,
                .InputStream = file.InputStream
                }
        request.Headers("Content-Type") = file.ContentType

        If disposition > "" Then
            Dim filename = Replace(file.FileName, """", "'") 'replace quotes
            request.Headers("Content-Disposition") = "inline; filename=""" & filename & """"
        End If

        Dim response = client.PutObject(request)
        'logger("uploaded to: " & request.Key)
        'logger("HttpStatusCode=" & response.HttpStatusCode)
        Return response
    End Function

    'alternative hi-level way to upload - use TransferUtility
    'Dim fileTransferUtility = New Amazon.S3.Transfer.TransferUtility(client)
    'fileTransferUtility.Upload()


    ''' <summary>
    ''' return signed url for the key with standard params: 10 min expiration
    ''' </summary>
    ''' <param name="key">relative to the S3Root</param>
    ''' <returns>url to download the </returns>
    ''' see for all the details and ability to override response headers https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/S3/MS3GetPreSignedURLGetPreSignedUrlRequest.html
    Public Function getSignedUrl(key As String) As String
        Dim request = New GetPreSignedUrlRequest With {
            .BucketName = Me.bucket,
            .Key = Me.root & key,
            .Expires = DateTime.Now.AddMinutes(10)
        }
        'or DateTime.UtcNow
        'sample code
        'request.ResponseHeaderOverrides.ContentType = "text/xml+zip"
        'request.ResponseHeaderOverrides.ContentDisposition = "attachment; filename=dispName.pdf"
        'request.ResponseHeaderOverrides.CacheControl = "No-cache"
        'request.ResponseHeaderOverrides.ContentLanguage = "mi, en"
        'request.ResponseHeaderOverrides.Expires = "Thu, 01 Dec 1994 16:00:00 GMT"
        'request.ResponseHeaderOverrides.ContentEncoding = "x-gzip"
        Return client.GetPreSignedURL(request)
    End Function

    ''' <summary>
    ''' delete one object or whole folder
    ''' </summary>
    ''' <param name="key">relative to the S3Root</param>
    ''' <returns>response of one object deletion or response of top folder delete</returns>
    ''' <remarks>RECURSIVE! for folders</remarks>
    Public Function deleteObject(key As String) As DeleteObjectResponse
        If Right(key, 1) = "/" Then
            'it's subfolder - delete all content first

            Dim listrequest As New ListObjectsRequest With {
                    .BucketName = Me.bucket,
                    .Prefix = key
                }
            Dim list As ListObjectsResponse = client.ListObjects(listrequest)
            For Each entry As S3Object In list.S3Objects
                deleteObject(entry.Key)
            Next
        End If

        Dim request As New DeleteObjectRequest With {
                    .BucketName = Me.bucket,
                    .Key = Me.root & key
                }

        Return client.DeleteObject(request)
    End Function

End Class
#End If