using System.Globalization;
using MgkctSchedule.Models;

namespace MgkctSchedule.Services;

public record DayLesson(int Number, DateTime Start, DateTime End, string Subject, string Room);
public record NotifyPlan(DateTime FireAt, DateTime Day);
public record NotifyText(string Title, string Collapsed, string Body);

/// <summary>Когда слать уведомление и что в нём написать (без зависимости от Android).</summary>
public static class NotifyLogic
{
    static readonly string[] DayNames =
        { "воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота" };
    static readonly string[] MonthsGen =
        { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };

    /// <summary>Пары дня с учётом подгруппы и звонков.</summary>
    public static List<DayLesson> LessonsFor(DaySchedule day, bool shortBells, int subgroup)
    {
        var result = new List<DayLesson>();
        var bells = Bells.For(day.Date.DayOfWeek, shortBells);
        foreach (var pair in day.Pairs.OrderBy(p => p.Number))
        {
            var entries = subgroup == 0
                ? pair.Entries
                : pair.Entries.Where(e => e.Subgroup == null || e.Subgroup == subgroup).ToList();
            if (entries.Count == 0) continue;
            var slot = bells.FirstOrDefault(b => b.Number == pair.Number);
            if (slot == null) continue;

            var subject = string.Join(" / ", entries.Select(e => e.Subject).Distinct());
            var rooms = string.Join("/", entries.Select(e => e.Room.Replace(" (к)", "")).Where(r => r.Length > 0).Distinct());
            result.Add(new DayLesson(pair.Number, day.Date.Date + slot.Start, day.Date.Date + slot.End, subject, rooms));
        }
        return result;
    }

    /// <summary>
    /// Ближайший день, у которого конец последней пары ещё впереди (или был недавно, но
    /// уведомление за него не отправлено). Сокращённые звонки (ручной флаг) считаются только на сегодня.
    /// </summary>
    public static NotifyPlan? NextPlan(IEnumerable<DaySchedule> days, DateTime now, bool shortToday, int subgroup, DateTime? lastNotified)
    {
        var byDate = new Dictionary<DateTime, DaySchedule>();
        foreach (var d in days) byDate[d.Date.Date] = d;

        for (int i = 0; i <= 14; i++)
        {
            var date = now.Date.AddDays(i);
            if (lastNotified != null && date <= lastNotified.Value.Date) continue;
            if (!byDate.TryGetValue(date, out var day)) continue;

            var lessons = LessonsFor(day, shortToday && date == now.Date, subgroup);
            if (lessons.Count == 0) continue;

            var fire = lessons.Max(l => l.End);
            if (fire < now.AddMinutes(-30)) continue;     // слишком давно, не беспокоим
            return new NotifyPlan(fire, date);
        }
        return null;
    }

    /// <summary>Первый день после указанного, в котором у группы есть пары (до 10 дней вперёд).</summary>
    public static (DateTime Date, List<DayLesson> Lessons)? NextStudyDay(IEnumerable<DaySchedule> days, DateTime after, int subgroup)
    {
        var byDate = new Dictionary<DateTime, DaySchedule>();
        foreach (var d in days) byDate[d.Date.Date] = d;

        for (int i = 1; i <= 10; i++)
        {
            var date = after.Date.AddDays(i);
            if (!byDate.TryGetValue(date, out var day)) continue;
            var lessons = LessonsFor(day, false, subgroup);   // на будущие дни - обычные звонки
            if (lessons.Count > 0) return (date, lessons);
        }
        return null;
    }

    public static string Plural(int n)
    {
        int m10 = n % 10, m100 = n % 100;
        if (m10 == 1 && m100 != 11) return "пара";
        if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return "пары";
        return "пар";
    }

    public static NotifyText BuildText(DateTime date, DateTime today, List<DayLesson> lessons)
    {
        var dayName = DayNames[(int)date.DayOfWeek];
        var dateText = $"{dayName}, {date.Day} {MonthsGen[date.Month - 1]}";
        var label = date.Date == today.Date.AddDays(1)
            ? "Завтра"
            : char.ToUpper(dayName[0], CultureInfo.InvariantCulture) + dayName.Substring(1);

        var first = lessons[0];
        return new NotifyText(
            Title: $"{label}: {lessons.Count} {Plural(lessons.Count)}",
            Collapsed: $"Первая в {first.Start:HH\\:mm} · {first.Subject}",
            Body: dateText + "\n" + string.Join("\n", lessons.Select(l =>
                $"{l.Number}. {l.Start:HH\\:mm} {l.Subject}" + (l.Room.Length > 0 ? $" · ауд. {l.Room}" : ""))));
    }
}
