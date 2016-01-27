' Demo model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class Demos
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "demos"
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

    'check if item exists for a given email
    Public Overrides Function is_exists(uniq_key As Object, not_id As Integer) As Boolean
        Return is_exists_byfield(uniq_key, not_id, "email")
    End Function
End Class
