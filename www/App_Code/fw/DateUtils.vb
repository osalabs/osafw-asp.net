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

        If String.IsNullOrEmpty(str) OrElse str = "0000-00-00" OrElse str = "0000-00-00 00:00:00" Then Return result
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
            result = tmpdate.Hour().ToString("00") & ":" & tmpdate.Minute().ToString("00")
        End If

        Return result
    End Function

    'return next day of week
    Public Shared Function nextDOW(whDayOfWeek As DayOfWeek, Optional theDate As DateTime = Nothing) As DateTime
        If theDate = Nothing Then theDate = DateTime.Today
        Dim d As DateTime = theDate.AddDays(whDayOfWeek - theDate.DayOfWeek)
        Return IIf(d <= theDate, d.AddDays(7), d)
    End Function

    Public Shared Function Unix2Date(unixTimeStamp As Double) As DateTime
        Dim result As DateTime = New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
        result = result.AddSeconds(unixTimeStamp).ToLocalTime()
        Return result
    End Function

    'convert .net date to javascript timestamp
    Public Shared Function Date2JsTimestamp(dt As Date) As Long
        Dim span As TimeSpan = New TimeSpan(Date.Parse("1/1/1970").Ticks)
        Dim time As Date = dt.Subtract(span)
        Return CLng(time.Ticks / 10000)
    End Function

End Class
