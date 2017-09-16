' Contact Us public controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports System.Net
Imports System.IO

Public Class ContactController
    Inherits FwController

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)

        base_url = "/Contact"
    End Sub

    Public Function IndexAction() As Hashtable
        Dim ps As Hashtable = New Hashtable

        Dim page As Hashtable = fw.model(Of Spages).oneByFullUrl(base_url)
        ps("page") = page
        Return ps
    End Function

    Public Function SentAction(Optional url As String = "") As Hashtable
        Dim ps As Hashtable = New Hashtable

        Dim page As Hashtable = fw.model(Of Spages).oneByFullUrl(base_url & "/Sent")
        ps("page") = page
        Return ps
    End Function

End Class

