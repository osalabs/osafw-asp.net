' UserLists model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2018 Oleg Savchuk www.osalabs.com

Public Class UserLists
    Inherits FwModel

    Public table_items As String = "user_lists_items"

    Public Sub New()
        MyBase.New()
        table_name = "user_lists"
    End Sub

    Function countItems(id As Integer) As Integer
        Return db.value(table_items, New Hashtable From {{"user_lists_id", id}}, "count(*)")
    End Function

    'list for select by entity and for only logged user
    Public Function listSelectByEntity(entity As String) As ArrayList
        Return db.array("select id, iname from " & table_name & " where status=0 and entity=" & db.q(entity) & " and add_users_id=" & fw.model(Of Users).meId & " order by iname")
    End Function

    Public Function listItemsById(id As Integer) As ArrayList
        Return db.array("select id, item_id from " & table_items & " where status=0 and user_lists_id=" & db.qi(id) & " order by id desc")
    End Function

    Public Function listForItem(entity As String, item_id As Integer) As ArrayList
        Return db.array("select t.id, t.iname, " & item_id & " as item_id, ti.id as is_checked from " & table_name & " t" &
                        " LEFT OUTER JOIN " & table_items & " ti ON (ti.user_lists_id=t.id and ti.item_id=" & item_id & " )" &
                        " where t.status=0 and t.entity=" & db.q(entity) &
                        " and t.add_users_id=" & fw.model(Of Users).meId &
                        " order by t.iname")
    End Function

    Public Overrides Sub delete(id As Integer, Optional is_perm As Boolean = False)
        If is_perm Then
            'delete list items first
            Dim where As New Hashtable
            where("user_lists_id") = id
            db.del(table_items, where)
        End If

        MyBase.delete(id, is_perm)
    End Sub

    Public Function oneItemsByUK(user_lists_id As Integer, item_id As Integer) As Hashtable
        Return db.row(table_items, New Hashtable From {{"user_lists_id", user_lists_id}, {"item_id", item_id}})
    End Function

    Public Overridable Sub deleteItems(id As Integer)
        Dim where As New Hashtable
        where("id") = id
        db.del(table_items, where)
        fw.logEvent(table_items & "_del", id)
    End Sub

    'add new record and return new record id
    Public Overridable Function addItems(user_lists_id As Integer, item_id As Integer) As Integer
        Dim item As New Hashtable
        item("user_lists_id") = user_lists_id
        item("item_id") = item_id
        item("add_users_id") = fw.model(Of Users).meId()

        Dim id As Integer = db.insert(table_items, item)
        fw.logEvent(table_items & "_add", id)
        Return id
    End Function

    'add or remove item from the list
    Public Function toggleItemList(user_lists_id As Integer, item_id As Integer) As Boolean
        Dim result = False
        Dim litem = oneItemsByUK(user_lists_id, item_id)
        If litem.Count > 0 Then
            'remove 
            deleteItems(litem("id"))
        Else
            'add new
            addItems(user_lists_id, item_id)
            result = True
        End If

        Return result
    End Function

    'add item to the list, if item not yet in the list
    Public Function addItemList(user_lists_id As Integer, item_id As Integer) As Boolean
        Dim result = False
        Dim litem = oneItemsByUK(user_lists_id, item_id)
        If litem.Count > 0 Then
            'do nothing
        Else
            'add new
            addItems(user_lists_id, item_id)
            result = True
        End If

        Return result
    End Function

    'delete item from the list
    Public Function delItemList(user_lists_id As Integer, item_id As Integer) As Boolean
        Dim result = False
        Dim litem = oneItemsByUK(user_lists_id, item_id)
        If litem.Count > 0 Then
            deleteItems(litem("id"))
            result = True
        End If

        Return result
    End Function

End Class
