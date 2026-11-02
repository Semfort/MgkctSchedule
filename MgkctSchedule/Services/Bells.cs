using MgkctSchedule.Models;

namespace MgkctSchedule.Services;

/// <summary>Расписание звонков. Если колледж поменяет время, правится только этот файл.</summary>
public static class Bells
{
    static TimeSpan T(string s) => TimeSpan.Parse(s);

    static BellSlot Full(int n, string s1, string e1, string s2, string e2) => new()
    {
        Number = n, Start = T(s1), End = T(e2), BreakStart = T(e1), BreakEnd = T(s2)
    };

    static BellSlot Short(int n, string s, string e) => new() { Number = n, Start = T(s), End = T(e) };

    static readonly BellSlot[] Weekday =
    {
        Full(1, "09:00", "09:45", "09:55", "10:40"),
        Full(2, "10:50", "11:35", "11:55", "12:40"),
        Full(3, "13:00", "13:45", "13:55", "14:40"),
        Full(4, "14:50", "15:35", "15:45", "16:30"),
        Full(5, "16:40", "17:25", "17:35", "18:20"),
        Full(6, "18:30", "19:15", "19:25", "20:10"),
    };

    static readonly BellSlot[] Saturday =
    {
        Full(1, "09:00", "09:45", "09:55", "10:40"),
        Full(2, "10:50", "11:35", "11:50", "12:35"),
        Full(3, "12:50", "13:35", "13:45", "14:30"),
        Full(4, "14:40", "15:25", "15:35", "16:20"),
        Full(5, "16:30", "17:15", "17:25", "18:10"),
        Full(6, "18:20", "19:05", "19:15", "20:00"),
    };

    static readonly BellSlot[] Shortened =
    {
        Short(1, "09:00", "10:00"),
        Short(2, "10:10", "11:10"),
        Short(3, "11:30", "12:30"),
        Short(4, "12:40", "13:40"),
        Short(5, "13:50", "14:50"),
        Short(6, "15:00", "16:00"),
    };

    public static BellSlot[] For(DayOfWeek day, bool shortened) =>
        shortened ? Shortened : day == DayOfWeek.Saturday ? Saturday : Weekday;
}
