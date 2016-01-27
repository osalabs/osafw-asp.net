' Settings model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Imports Microsoft.VisualBasic

Public Class Settings
    Inherits FwModel

    Public Sub New()
        MyBase.New()
        table_name = "settings"
    End Sub

    ''' <summary>
    ''' Return site setting by icode, static function for easier use: Settings.read('icode')
    ''' </summary>
    ''' <param name="icode"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Shared Function read(icode As String) As String
        Return fw.Current.model(Of Settings).get_value(icode)
    End Function
    ''' <summary>
    ''' Read integer value from site settings
    ''' </summary>
    ''' <param name="icode"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Shared Function readi(icode As String) As Integer
        Return Utils.f2int(read(icode))
    End Function
    ''' <summary>
    ''' Read date value from site settings
    ''' </summary>
    ''' <param name="icode"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Shared Function readd(icode As String) As Object
        Return Utils.f2date(read(icode))
    End Function

    ''' <summary>
    ''' Change site setting by icode, static function for easier use: Settings.write('icode', value)
    ''' </summary>
    ''' <param name="icode"></param>
    ''' <remarks></remarks>
    Public Shared Sub write(icode As String, value As String)
        fw.Current.model(Of Settings).set_value(icode, value)
    End Sub


    'just return first row by icode field
    Public Function one_by_icode(icode As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where("icode") = icode
        Return db.row(table_name, where)
    End Function

    Public Function get_value(icode As String) As String
        Return one_by_icode(icode)("ivalue")
    End Function
    Public Sub set_value(icode As String, ivalue As String)
        Dim item As Hashtable = Me.one_by_icode(icode)
        Dim fields As New Hashtable
        If item.ContainsKey("id") Then
            'exists - update
            fields("ivalue") = ivalue
            update(item("id"), fields)
        Else
            'not exists - add new
            fields("icode") = icode
            fields("ivalue") = ivalue
            fields("is_user_edit") = 0 'all auto-added settings is not user-editable by default
            Me.add(fields)
        End If
    End Sub

    Public Function full_name(id As Object) As String
        Dim result As String = ""
        id = Utils.f2int(id)

        If id > 0 Then
            Dim hU As Hashtable = one(id)
            result = hU("iname")
        End If

        Return result
    End Function

    'check if item exists for a given icode
    Public Overrides Function is_exists(uniq_key As Object, not_id As Integer) As Boolean
        Return is_exists_byfield(uniq_key, not_id, "icode")
    End Function

End Class
