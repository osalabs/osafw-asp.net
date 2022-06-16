' Events Log Admin  controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class AdminEventsController
    Inherits FwAdminController
    Public Shared Shadows access_level As Integer = 100

    Protected model As New FwEvents
    Protected model_users As New Users

    Public Overrides Sub init(fw As FW)
        MyBase.init(fw)
        model0 = model
        model.init(fw)
        model_users.init(fw)
        required_fields = "iname" 'default required fields, space-separated
        base_url = "/Admin/Events" 'base url for the controller

        search_fields = "!item_id iname fields"
        list_sortdef = "iname asc"
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time")

        list_view = model.log_table_name
    End Sub

    Public Overrides Function initFilter(Optional session_key As String = Nothing) As Hashtable
        MyBase.initFilter(session_key)

        If Not reqs("dofilter") > "" AndAlso list_filter("date") = "" Then
            list_filter("date") = DateUtils.Date2Str(Now())
        End If

    End Function

    Public Overrides Sub setListSearch()
        MyBase.setListSearch()

        If list_filter("events_id") > "" Then
            list_where &= " and events_id = " & db.qi(list_filter("events_id"))
        End If
        If list_filter("users_id") > "" Then
            list_where &= " and add_users_id = " & db.qi(list_filter("users_id"))
        End If
        If list_filter("date") > "" Then
            Dim d As String = db.qone(model.log_table_name, "add_time", list_filter("date"))
            list_where &= " and add_time >= " & d & " and add_time < DATEADD(DAY, 1, " & d & ")"
        End If
    End Sub

    Public Overrides Sub setListSearchStatus()
        'no status
    End Sub


    Public Overrides Sub getListRows()
        MyBase.getListRows()

        For Each row As Hashtable In list_rows
            logger(row)
            row("user") = model_users.one(Utils.f2int(row("add_users_id")))
            row("event") = model.one(Utils.f2int(row("events_id")))
        Next
    End Sub

    Public Overrides Function IndexAction() As Hashtable
        Dim ps = MyBase.IndexAction()

        ps("filter_select_events") = model.listSelectOptions()
        ps("filter_select_users") = model_users.listSelectOptions()

        Return ps
    End Function

End Class
