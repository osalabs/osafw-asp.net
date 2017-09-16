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

End Class
