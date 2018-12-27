' Manage  controller for Developers
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018  Oleg Savchuk www.osalabs.com

Public Class DevManageController
    Inherits FwController
    Public Shared Shadows access_level As Integer = 100

    Public Function IndexAction() As Hashtable
        Dim ps As New Hashtable

        Return ps
    End Function

End Class
