' UserFilters model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2021 Oleg Savchuk www.osalabs.com

Public Class UserFilters
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "user_filters"
    End Sub

    'list for select by icode and only for logged user OR active system filters
    Public Function listSelectByIcode(icode As String) As ArrayList
        Return db.array("select id, iname from " & table_name & " where status=0 and icode=" & db.q(icode) & " and (is_system=1 OR add_users_id=" & fw.model(Of Users).meId & ") order by is_system desc, iname")
    End Function

End Class
