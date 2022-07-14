' Att Categories Dictionary Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class AdminAttCategoriesController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 80

    Protected model As AttCategories

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = fw.model(Of AttCategories)()
        model = model0

        'initialization
        base_url = "/Admin/AttCategories"
        required_fields = "iname"
        save_fields = "icode iname idesc status"

        search_fields = "iname idesc"
        list_sortdef = "iname asc"   'default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id icode|icode iname|iname add_time|add_time status|status")
    End Sub
End Class
