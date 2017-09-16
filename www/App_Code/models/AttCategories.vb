' Att Categories Dictionary model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class AttCategories
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "att_categories"
    End Sub

    'just return first row by iname field (you may want to make it unique)
    Public Overridable Function oneByIcode(icode As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where("icode") = icode
        Return db.row(table_name, where)
    End Function

End Class
