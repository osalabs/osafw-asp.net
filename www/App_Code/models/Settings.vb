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
        Return FW.Current.model(Of Settings).getValue(icode)
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
        FW.Current.model(Of Settings).setValue(icode, value)
    End Sub


    'just return first row by icode field
    Public Function oneByIcode(icode As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where("icode") = icode
        Return db.row(table_name, where)
    End Function

    Public Function getValue(icode As String) As String
        Return oneByIcode(icode)("ivalue")
    End Function
    Public Sub setValue(icode As String, ivalue As String)
        Dim item As Hashtable = Me.oneByIcode(icode)
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

    'check if item exists for a given icode
    Public Overrides Function isExists(uniq_key As Object, not_id As Integer) As Boolean
        Return isExistsByField(uniq_key, not_id, "icode")
    End Function

End Class
