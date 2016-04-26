' Users model class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class Users
    Inherits FwModel
    'ACL constants
    Public Const ACL_VISITOR As Integer = -1
    Public Const ACL_MEMBER As Integer = 0
    Public Const ACL_ADMIN As Integer = 100

    Public Sub New()
        MyBase.New()
        table_name = "users"
        csv_export_fields = "id,fname,lname,email,add_time"
        csv_export_headers = "id,First Name,Last Name,Email,Registered"
    End Sub

    Public Function me_id() As Integer
        If IsNothing(fw.SESSION("user")) OrElse Not fw.SESSION("user").ContainsKey("id") Then Return 0
        Return Utils.f2int(fw.SESSION("user")("id"))
    End Function

    Public Function one_by_email(email As String) As Hashtable
        Dim where As Hashtable = New Hashtable
        where("email") = email
        Dim hU As Hashtable = db.row(table_name, where)
        Return hU
    End Function

    Public Function full_name(id As Object) As String
        Dim result As String = ""
        id = Utils.f2int(id)

        If id > 0 Then
            Dim hU As Hashtable = one(id)
            result = hU("fname") & "  " & hU("lname")
        End If

        Return result
    End Function

    'check if user exists for a given email
    Public Overrides Function is_exists(uniq_key As Object, not_id As Integer) As Boolean
        Dim val As String = db.value("select 1 from users where email=" & db.q(uniq_key) & " and id <>" & db.qi(not_id))
        If val = "1" Then
            Return True
        Else
            Return False
        End If
    End Function

    'fill the session and do all necessary things just user authenticated (and before redirect
    Public Function do_login(id As Integer) As Boolean
        Dim hU As Hashtable = one(id)

        fw.SESSION.Clear()
        fw.SESSION("is_logged", True)
        fw.SESSION("XSS", Utils.get_rand_str(16))
        fw.SESSION("login", hU("email"))
        fw.SESSION("access_level", Utils.f2int(hU("access_level")))
        fw.SESSION("user", hU)

        fw.log_event("login", id)
        'update login info
        Dim fields As New Hashtable
        fields("login_time") = Now()
        Me.update(id, fields)
        Return True
    End Function

    Public Function session_reload() As Boolean
        Dim hU As Hashtable = one(me_id())

        fw.SESSION("login", hU("email"))
        fw.SESSION("access_level", Utils.f2int(hU("access_level")))
        fw.SESSION("user", hU)

        Return True
    End Function

    'return standard list of id,iname where status=0 order by iname
    Public Overrides Function list() As ArrayList
        Dim sql As String = "select id, fname+' '+lname as iname from " & table_name & " where status=0 order by fname, lname"
        Return db.array(sql)
    End Function

    Public Overrides Function get_select_options(sel_id As String) As String
        Return FormUtils.select_options_db(Me.list(), sel_id)
    End Function

    ''' <summary>
    ''' check if current user acl is enough. throw exception or return false if user's acl is not enough
    ''' </summary>
    ''' <param name="acl">minimum required access level</param>
    Public Function check_access(acl As Integer, Optional is_die As Boolean = True) As Boolean
        Dim users_acl As Integer = Utils.f2int(fw.SESSION("access_level"))

        'check access
        If users_acl < acl Then
            If is_die Then Throw New ApplicationException("Access Denied")
            Return False
        End If

        Return True
    End Function

End Class
