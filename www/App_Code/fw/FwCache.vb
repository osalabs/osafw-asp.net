' Fw Cache class
' Application-level cache
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class FwCache
    Public Shared cache As New Hashtable 'app level cache
    Public request_cache As New Hashtable 'request level cache

    Public Shared Function get_value(key As String) As Object
        Return cache(key)
    End Function

    Public Shared Sub set_value(key As String, value As Object)
        cache(key) = value
    End Sub

    'remove one key from cache
    Public Shared Sub remove(key As String)
        cache.Remove(key)
    End Sub

    'clear whole cache
    Public Shared Sub clear()
        cache.Clear()
    End Sub

    '******** request-level cache ***********

    Public Function get_request_value(key As String) As Object
        Return request_cache(key)
    End Function
    Public Sub set_request_value(key As String, value As Object)
        request_cache(key) = value
    End Sub
    'remove one key from request cache
    Public Sub request_remove(key As String)
        request_cache.Remove(key)
    End Sub
    'clear whole request cache
    Public Sub request_clear()
        request_cache.Clear()
    End Sub

End Class
