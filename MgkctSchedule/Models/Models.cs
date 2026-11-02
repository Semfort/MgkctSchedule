namespace MgkctSchedule.Models;

/// <summary>Одно занятие внутри ячейки таблицы (у подгрупп в ячейке их несколько).</summary>
public class LessonEntry
{
    public int? Subgroup { get; set; }          // null = вся группа
    public string Subject { get; set; } = "";
    public string Type { get; set; } = "";      // Лек, ЛР, Пр-ка ...
    public string Teacher { get; set; } = "";
    public string Room { get; set; } = "";
}

/// <summary>Строка таблицы: номер пары + занятия (пусто = нет пары).</summary>
public class PairSlot
{
    public int Number { get; set; }
    public List<LessonEntry> Entries { get; set; } = new();
}

public class DaySchedule
{
    public DateTime Date { get; set; }
    public List<PairSlot> Pairs { get; set; } = new();
}

public class ScheduleCache
{
    public DateTime FetchedAt { get; set; }
    public List<DaySchedule> Days { get; set; } = new();
}

/// <summary>Время пары. Break* - перерыв между двумя половинами пары (у сокращённых его нет).</summary>
public class BellSlot
{
    public int Number { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
}
