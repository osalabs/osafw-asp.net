' Date framework utils
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2013 Oleg Savchuk www.osalabs.com

Public Class DateUtils

    Public Shared Function Date2SQL(ByVal d As DateTime) As String
        Return d.Year() & "-" & d.Month() & "-" & d.Day()
    End Function

    'IN: VB Date
    'OUT: MM/DD/YYYY
    Public Shared Function Date2Str(ByVal d As DateTime) As String
        Return d.Month() & "/" & d.Day() & "/" & d.Year()
    End Function

    Public Shared Function SQL2Date(ByVal str As String) As DateTime
        Dim result As DateTime

        If str = "" OrElse str = "0000-00-00" OrElse str = "0000-00-00 00:00:00" Then Return result
        'yyyy-mm-dd
        Dim m As Match = Regex.Match(str, "^(\d+)-(\d+)-(\d+)")
        'hh:mm:ss
        Dim m2 As Match = Regex.Match(str, "(\d+):(\d+):(\d+)$")

        If m2.Success Then
            result = New DateTime(m.Groups(1).Value, m.Groups(2).Value, m.Groups(3).Value, CInt(m2.Groups(1).Value), CInt(m2.Groups(2).Value), CInt(m2.Groups(3).Value))
        Else
            result = New DateTime(m.Groups(1).Value, m.Groups(2).Value, m.Groups(3).Value)
        End If

        Return result
    End Function

    'IN: MM/DD/YYYY[ HH:MM:SS]
    'OUT: YYYY-MM-DD[ HH:MM:SS]
    Public Shared Function Str2SQL(str As String, Optional is_time As Boolean = False) As String
        Dim result As String = ""
        Dim tmpdate As DateTime
        If DateTime.TryParse(str, tmpdate) Then
            Dim format As String = "yyyy-MM-dd HH:mm:ss"
            If Not is_time Then
                format = "yyyy-MM-dd"
            End If
            result = tmpdate.ToString(format, System.Globalization.DateTimeFormatInfo.InvariantInfo)
        End If

        Return result
    End Function

    'IN: datetime string
    'OUT: HH:MM
    Public Shared Function Date2TimeStr(str As String) As String
        Dim result As String = ""
        Dim tmpdate As DateTime
        If DateTime.TryParse(str, tmpdate) Then
            result = tmpdate.Hour() & ":" & tmpdate.Minute()
        End If

        Return result
    End Function

    'return next day of week
    Public Shared Function nextDOW(whDayOfWeek As DayOfWeek, Optional theDate As DateTime = Nothing) As DateTime
        If theDate = Nothing Then theDate = DateTime.Today
        Dim d As DateTime = theDate.AddDays(whDayOfWeek - theDate.DayOfWeek)
        Return IIf(d <= theDate, d.AddDays(7), d)
    End Function
End Class
