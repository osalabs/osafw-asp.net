Call DoBackup()

Sub DoBackup()

        'Force the script to finish on an error.
        'On Error Resume Next

        'Declare variables
        Dim objRequest
        Dim URL
        Dim DataToSend : DataToSend = "do=1"

        Set objRequest = CreateObject("MSXML2.ServerXMLHTTP")
        'Set objRequest = CreateObject("Microsoft.XMLHTTP")
        'Set objRequest = CreateObject("MSXML2.XMLHTTP.3.0")

        'Put together the URL link appending the Variables.
        URL = "http://localhost/Sys/Backup"
        'URL = "http://localhost:52144/Sys/Backup"

        'Open the HTTP request and pass the URL to the objRequest object
        objRequest.open "POST", URL , false

        'Send the HTML Request
        objRequest.Send DataToSend

        'Set the object to nothing
        Set objRequest = Nothing

End Sub