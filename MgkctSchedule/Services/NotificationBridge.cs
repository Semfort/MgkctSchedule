namespace MgkctSchedule.Services;

/// <summary>Мост к Android-уведомлениям (на других платформах ничего не делает).</summary>
public static class NotificationBridge
{
    public static void Reschedule()
    {
#if ANDROID
        try { MgkctSchedule.Notifications.TomorrowNotifier.Reschedule(global::Android.App.Application.Context); }
        catch { }
#endif
    }

    /// <summary>Android 13+: запросить разрешение на уведомления.</summary>
    public static void EnsurePermission()
    {
#if ANDROID
        try { MgkctSchedule.Notifications.TomorrowNotifier.RequestPermission(); }
        catch { }
#endif
    }

    /// <summary>Пробное уведомление. Возвращает false, если уведомления запрещены.</summary>
    public static bool ShowTest()
    {
#if ANDROID
        try { return MgkctSchedule.Notifications.TomorrowNotifier.ShowTest(global::Android.App.Application.Context); }
        catch { return false; }
#else
        return false;
#endif
    }
}
