' Categories model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class Categories
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "categories"
    End Sub

    Public Function full_name(id As Object) As String
        Dim result As String = ""
        id = Utils.f2int(id)

        If id > 0 Then
            Dim hU As Hashtable = one(id)
            result = hU("iname")
        End If

        Return result
    End Function

End Class
