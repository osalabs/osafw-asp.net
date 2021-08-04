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
    Public Overrides Function oneByIcode(screen As String) As Hashtable
        Return db.row(table_name, New Hashtable From {{field_add_users_id, fw.model(Of Users).meId}, {field_icode, screen}})
    End Function

    'update screen fields for logged user
    'return user_views.id
    Public Function updateByIcode(screen As String, fields As String) As Integer
        Dim result = 0
        Dim item = oneByIcode(screen)
        If item.Count > 0 Then
            'exists
            result = item(field_id)
            update(item(field_id), New Hashtable From {{"fields", fields}})
        Else
            'new
            result = add(New Hashtable From {
                    {field_icode, screen},
                    {"fields", fields},
                    {field_add_users_id, fw.model(Of Users).meId}
                })
        End If
        Return result
    End Function

    'list for select by entity and only for logged user OR active system views
    Public Function listSelectByIcode(entity As String) As ArrayList
        Return db.array("select id, iname from " & table_name & " where status=0 and icode=" & db.q(entity) & " and (is_system=1 OR add_users_id=" & fw.model(Of Users).meId & ") order by is_system desc, iname")
    End Function

End Class
