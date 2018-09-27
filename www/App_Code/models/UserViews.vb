' User Custom List Views model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class UserViews
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "user_views"
    End Sub

    'return screen record for logged user
    Public Function oneByScreen(screen As String) As Hashtable
        Return db.row(table_name, New Hashtable From {{"add_users_id", fw.model(Of Users).meId}, {"screen", screen}})
    End Function

    'update screen fields for logged user
    'return user_views.id
    Public Function updateByScreen(screen As String, fields As String) As Integer
        Dim result = 0
        Dim item = oneByScreen(screen)
        If item.Count > 0 Then
            'exists
            result = item("id")
            update(item("id"), New Hashtable From {{"fields", fields}})
        Else
            'new
            result = add(New Hashtable From {
                    {"screen", screen},
                    {"fields", fields},
                    {"add_users_id", fw.model(Of Users).meId}
                })
        End If
        Return result
    End Function
End Class
