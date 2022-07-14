' Fw Cache class
' Application-level cache
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class FwCache
    Public Shared cache As New Hashtable 'app level cache
    Private Shared ReadOnly locker As New Object

    Public request_cache As New Hashtable 'request level cache

    Public Shared Function getValue(key As String) As Object
        Dim result = cache(key)
        If result IsNot Nothing Then
            Dim t = result.GetType()
            If t.IsSerializable Then
                result = Utils.deserialize(result)
            End If
        End If
        Return result
    End Function

    Public Shared Sub setValue(key As String, value As Object)
        SyncLock locker
            If value Is Nothing Then
                cache(key) = value
            Else
                Dim t = value.GetType()
                If t.IsSerializable Then
                    'serialize in cache because when read - need object clone, not original object
                    cache(key) = Utils.serialize(value)
                Else
                    cache(key) = value
                End If
            End If
        End SyncLock
    End Sub

    'remove one key from cache
    Public Shared Sub remove(key As String)
        SyncLock locker
            cache.Remove(key)
        End SyncLock
    End Sub

    'clear whole cache
    Public Shared Sub clear()
        SyncLock locker
            cache.Clear()
        End SyncLock
    End Sub

    '******** request-level cache ***********

    Public Function getRequestValue(key As String) As Object
        Dim result = request_cache(key)
        If result IsNot Nothing Then
            Dim t = result.GetType()
            If t.IsSerializable Then
                result = Utils.deserialize(result)
            End If
        End If
        Return result
    End Function
    Public Sub setRequestValue(key As String, value As Object)
        If value Is Nothing Then
            request_cache(key) = value
        Else
            Dim t = value.GetType()
            If t.IsSerializable Then
                'serialize in cache because when read - need object clone, not original object
                request_cache(key) = Utils.serialize(value)
            Else
                request_cache(key) = value
            End If
        End If
    End Sub
    'remove one key from request cache
    Public Sub requestRemove(key As String)
        request_cache.Remove(key)
    End Sub


    ''' <summary>
    ''' remove all keys with prefix from the request cache
    ''' </summary>
    ''' <param name="prefix">prefix key</param>
    Public Sub requestRemoveWithPrefix(prefix As String)
        Dim plen = prefix.Length
        For Each key As String In New ArrayList(request_cache.Keys)
            If Left(key, plen) = prefix Then
                request_cache.Remove(key)
            End If
        Next
    End Sub

    'clear whole request cache
    Public Sub requestClear()
        request_cache.Clear()
    End Sub

End Class
