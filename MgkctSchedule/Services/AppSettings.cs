namespace MgkctSchedule.Services;

/// <summary>Настройки приложения (хранятся в Preferences, общие для приложения, виджета и уведомлений).</summary>
public static class AppSettings
{
    public static bool ShortBells
    {
        get => Preferences.Default.Get("short_bells", false);
        set => Preferences.Default.Set("short_bells", value);
    }

    /// <summary>Номер группы, как на сайте (например "89" или "93*").</summary>
    public static string Group
    {
        get => Preferences.Default.Get("group", "89");
        set
        {
            if (value == Group) return;
            Preferences.Default.Set("group", value);
            LastNotifiedDay = null;   // у новой группы своё расписание - сбрасываем отметку об уведомлении
        }
    }

    /// <summary>0 = все подгруппы, 1 или 2.</summary>
    public static int Subgroup
    {
        get => Preferences.Default.Get("subgroup", 0);
        set => Preferences.Default.Set("subgroup", value);
    }

    /// <summary>Присылать расписание на завтра после последней пары.</summary>
    public static bool NotifyTomorrow
    {
        get => Preferences.Default.Get("notify_tomorrow", false);
        set => Preferences.Default.Set("notify_tomorrow", value);
    }

    /// <summary>День (yyyy-MM-dd), за который уведомление уже отправлено.</summary>
    public static DateTime? LastNotifiedDay
    {
        get => DateTime.TryParseExact(Preferences.Default.Get("notify_last_day", ""), "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
        set => Preferences.Default.Set("notify_last_day", value?.ToString("yyyy-MM-dd") ?? "");
    }

    public static event Action? Changed;

    /// <summary>Вызывать после изменения настроек: обновит виджет, уведомления и экран.</summary>
    public static void RaiseChanged()
    {
        WidgetBridge.Refresh();
        NotificationBridge.Reschedule();
        Changed?.Invoke();
    }
}
