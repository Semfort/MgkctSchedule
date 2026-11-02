using System.Text.Json;
using MgkctSchedule.Models;
using Microsoft.Maui.Storage;

namespace MgkctSchedule.Services;

public class ScheduleService
{
    /// <summary>Группы, доступные в настройках (на сайте 93 и 94 помечены звёздочкой).</summary>
    public static readonly string[] AvailableGroups =
    {
        "80", "81", "82", "83", "84", "85", "86", "87", "88", "89", "90",
        "91", "92", "93*", "94*", "95", "96", "97", "98", "99", "100"
    };

    public string Group { get; }

    public ScheduleService() : this(AppSettings.Group) { }
    public ScheduleService(string group) { Group = group; }

    string Url => "https://mgkct.minskedu.gov.by/personnel/for-students/weekly-timetable?group=" + Uri.EscapeDataString(Group);

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/124.0 Mobile Safari/537.36");
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9");
        return c;
    }

    // у каждой группы свой файл кэша
    string CachePath => Path.Combine(FileSystem.AppDataDirectory, $"schedule_{Group.Replace("*", "s")}.json");

    /// <summary>Скачивает страницу, вливает новые дни в кэш и сохраняет его.</summary>
    public async Task<ScheduleCache> RefreshAsync(CancellationToken ct = default)
    {
        var html = await Http.GetStringAsync(Url, ct);
        var fresh = ScheduleParser.ParseGroup(html, Group);

        var cache = LoadCache();
        var map = new Dictionary<DateTime, DaySchedule>();
        foreach (var d in cache.Days) map[d.Date.Date] = d;
        foreach (var d in fresh) map[d.Date.Date] = d;

        // храним не больше ~4 месяцев истории
        var limit = DateTime.Today.AddDays(-120);
        foreach (var old in map.Keys.Where(k => k < limit).ToList()) map.Remove(old);

        cache.FetchedAt = DateTime.Now;
        cache.Days = map.Values.OrderBy(d => d.Date).ToList();
        SaveCache(cache);
        return cache;
    }

    public ScheduleCache LoadCache()
    {
        try
        {
            if (File.Exists(CachePath))
                return JsonSerializer.Deserialize<ScheduleCache>(File.ReadAllText(CachePath)) ?? new();
        }
        catch { /* битый кэш игнорируем */ }
        return new();
    }

    public void SaveCache(ScheduleCache cache)
    {
        try { File.WriteAllText(CachePath, JsonSerializer.Serialize(cache)); }
        catch { }
        WidgetBridge.Refresh();
        NotificationBridge.Reschedule();
    }
}
