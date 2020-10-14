' Test Page for Logged user controller
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Imports System.Net
Imports System.IO
Imports System.Collections.Generic

Public Class TestController
    Inherits FwController

    Public Sub IndexAction()
        Dim hf As Hashtable = New Hashtable

        hf("memory_var") = "this is memory var"
        hf("markdown_var") = "this is **markdown** string"
        hf("zero") = 0
        hf("one") = 1
        hf("two") = 2
        hf("zerostr") = "0"
        hf("onestr") = "1"
        hf("truestr") = "true"
        hf("falsestr") = "false"
        hf("emptystr") = ""
        hf("abc") = "abc"
        hf("minusone") = -1
        hf("booltrue") = True
        hf("boolfalse") = False
        hf("hashtable") = New Hashtable From {
                {"key1", "value1"},
                {"key2", "value2"}
            }

        hf("now") = Now()


        Dim al As New ArrayList
        For i As Integer = 1 To 3
            Dim h As New Hashtable
            h("iname") = "line " & i
            al.Add(h)
        Next
        hf("repeat3_dr") = al

        Dim al100 As New ArrayList
        For i As Integer = 1 To 100
            Dim h As New Hashtable
            h("iname") = i
            al100.Add(h)
        Next
        hf("repeat100_dr") = al100
        hf("myself") = hf.Clone()

        fw.parser(hf)
    End Sub

    Public Sub BenchAction()
        rw("hello world")
    End Sub

    Public Sub ExceptionAction()
        Throw New ApplicationException("test")
    End Sub

End Class

