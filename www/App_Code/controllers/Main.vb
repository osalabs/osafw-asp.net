' Main Page for Logged user controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class MainController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 0

    Public Function IndexAction() As Hashtable
        Dim ps As Hashtable = New Hashtable

        Return ps
    End Function

End Class

