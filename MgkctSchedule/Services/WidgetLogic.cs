using MgkctSchedule.Models;

namespace MgkctSchedule.Services;

public class WidgetRow
{
    public string Title { get; set; } = "";
    public string Sub { get; set; } = "";
    public bool IsCurrent { get; set; }
}

public class WidgetModel
{
    public string Header { get; set; } = "";
    public string EmptyText { get; set; } = "";
    public List<WidgetRow> Rows { get; } = new();
    /// <summary>Когда виджету нужно перерисоваться в следующий раз.</summary>
    public DateTime NextChange { get; set; }
}

/// <summary>
/// Что показывать на виджете. Идёт пара: она + следующая (2 строки).
/// Иначе: ближайшие 3 пары (сегодня, а если сегодня всё кончилось, то завтра и дальше).
/// </summary>
public static class WidgetLogic
{
    static readonly string[] Short = { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };

    record Lesson(DateTime Start, DateTime End, string Subject, string Room);

    public static WidgetModel Build(IReadOnlyCollection<DaySchedule> days, DateTime now, bool shortToday, int subgroup)
    {
        var lessons = new List<Lesson>();
        foreach (var day in days.Where(d => d.Date.Date >= now.Date).OrderBy(d => d.Date))
        {
            // сокращённые звонки - ручной флаг, действует только на сегодня
            var bells = Bells.For(day.Date.DayOfWeek, shortToday && day.Date.Date == now.Date);
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
                lessons.Add(new Lesson(day.Date.Date + slot.Start, day.Date.Date + slot.End, subject, rooms));
            }
        }

        var model = new WidgetModel();

        // когда пересчитывать: ближайшее начало/конец пары, но не реже чем раз в час
        var boundaries = lessons.SelectMany(l => new[] { l.Start, l.End }).Where(t => t > now).ToList();
        var next = boundaries.Count > 0 ? boundaries.Min() : now.Date.AddDays(1).AddMinutes(5);
        model.NextChange = next < now.AddHours(1) ? next : now.AddHours(1);

        var current = lessons.FirstOrDefault(l => l.Start <= now && now < l.End);
        var upcoming = lessons.Where(l => l.Start > now).ToList();
        var pick = current != null
            ? new[] { current }.Concat(upcoming.Take(1)).ToList()
            : upcoming.Take(3).ToList();

        if (pick.Count == 0)
        {
            model.Header = "Расписание";
            model.EmptyText = days.Count == 0
                ? "Откройте приложение, чтобы загрузить расписание"
                : "Ближайших пар нет";
            return model;
        }

        if (current != null) model.Header = "Сейчас";
        else
        {
            var d = pick[0].Start.Date;
            model.Header = d == now.Date ? "Дальше сегодня"
                         : d == now.Date.AddDays(1) ? "Завтра"
                         : $"{Short[(int)d.DayOfWeek]} {d:dd.MM}";
        }

        foreach (var l in pick)
        {
            var day = l.Start.Date == now.Date ? "" : Short[(int)l.Start.DayOfWeek] + " ";
            model.Rows.Add(new WidgetRow
            {
                Title = l.Subject,
                Sub = $"{day}{l.Start:HH\\:mm}–{l.End:HH\\:mm}" + (l.Room.Length > 0 ? $" · {l.Room}" : ""),
                IsCurrent = l == current
            });
        }
        return model;
    }
}
