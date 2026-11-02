using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MgkctSchedule.Models;

namespace MgkctSchedule.Services;

/// <summary>Достаёт из страницы weekly-timetable таблицу нужной группы.</summary>
public static class ScheduleParser
{
    static readonly Regex DateRx = new(@"(\d{2})\.(\d{2})\.(\d{4})", RegexOptions.Compiled);
    static readonly Regex SubgroupRx = new(@"^(\d)\.\s*(.+)$", RegexOptions.Compiled);
    static readonly Regex BrRx = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex TagRx = new(@"<[^>]+>", RegexOptions.Compiled);
    static readonly Regex SpaceRx = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// На странице у группы может быть несколько блоков (текущая и следующая неделя):
    /// каждый - свой заголовок "Группа - N", дата-диапазон и таблица. Берём все.
    /// </summary>
    public static List<DaySchedule> ParseGroup(string html, string group)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var headingRx = new Regex($@"^Группа\s*-\s*{Regex.Escape(group)}$", RegexOptions.IgnoreCase);
        var result = new List<DaySchedule>();
        bool found = false, want = false;

        foreach (var n in doc.DocumentNode.Descendants())
        {
            if (n.Name == "h2")
            {
                want = headingRx.IsMatch(Clean(n.InnerText));
                if (want) found = true;
            }
            else if (want && n.Name == "table")
            {
                result.AddRange(ParseTable(n));
                want = false;
            }
        }

        if (!found)
            throw new InvalidDataException($"На странице не найдена группа {group}");
        if (result.Count == 0)
            throw new InvalidDataException($"Для группы {group} нет таблицы расписания");
        return result;
    }

    static List<DaySchedule> ParseTable(HtmlNode table)
    {
        var dates = new List<DateTime>();
        var days = new List<DaySchedule>();

        foreach (var tr in table.Descendants("tr"))
        {
            // шапка с датами: "Понедельник, 05.10.2026"
            if (dates.Count == 0)
            {
                foreach (var th in tr.Elements("th"))
                {
                    var m = DateRx.Match(Clean(th.InnerText));
                    if (m.Success)
                        dates.Add(new DateTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value)));
                }
                if (dates.Count > 0)
                    days.AddRange(dates.Select(d => new DaySchedule { Date = d }));
                continue;
            }

            // строка пары: <th>1</th> + по две <td> на день
            var first = tr.Element("th");
            if (first == null || !int.TryParse(Clean(first.InnerText), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                continue;
            var tds = tr.Elements("td").ToList();
            if (tds.Count < dates.Count * 2) continue;

            for (int i = 0; i < dates.Count; i++)
            {
                var entries = ParseCell(Lines(tds[i * 2]), Lines(tds[i * 2 + 1]));
                days[i].Pairs.Add(new PairSlot { Number = number, Entries = entries });
            }
        }
        return days;
    }

    static string Clean(string s) =>
        SpaceRx.Replace(HtmlEntity.DeEntitize(s).Replace(' ', ' '), " ").Trim();

    static List<string> Lines(HtmlNode cell) =>
        BrRx.Split(cell.InnerHtml)
            .Select(p => Clean(TagRx.Replace(p, "")))
            .Where(s => s.Length > 0)
            .ToList();

    static bool IsType(string s) => s.StartsWith("(") && s.EndsWith(")");

    /// <summary>
    /// Ячейка: [название, (вид), преподаватель] повторяется для каждой подгруппы,
    /// "1.Название" - подгруппа 1. Аудитории - в соседней ячейке в том же порядке.
    /// </summary>
    static List<LessonEntry> ParseCell(List<string> lines, List<string> rooms)
    {
        var raw = new List<(string subject, string type, string teacher)>();
        int i = 0, n = lines.Count;
        while (i < n)
        {
            string name = lines[i++], type = "", teacher = "";
            if (i < n && IsType(lines[i]))
            {
                type = lines[i++].Trim('(', ')', ' ');
                var teachers = new List<string>();
                // строки преподавателей идут до следующего "название + (вид)"
                while (i < n && !(i + 1 < n && IsType(lines[i + 1])))
                    teachers.Add(lines[i++]);
                teacher = string.Join(", ", teachers);
            }
            raw.Add((name, type, teacher));
        }

        var result = new List<LessonEntry>();
        for (int k = 0; k < raw.Count; k++)
        {
            var (subject, type, teacher) = raw[k];
            int? sub = null;
            var m = SubgroupRx.Match(subject);
            if (m.Success) { sub = int.Parse(m.Groups[1].Value); subject = m.Groups[2].Value.Trim(); }

            string room = rooms.Count == raw.Count ? rooms[k]
                        : rooms.Count == 1 ? rooms[0]
                        : k < rooms.Count ? rooms[k] : "";
            if (room == "-") room = "";

            result.Add(new LessonEntry { Subgroup = sub, Subject = subject, Type = type, Teacher = teacher, Room = room });
        }
        return result;
    }
}
