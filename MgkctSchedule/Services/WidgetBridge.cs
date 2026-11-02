namespace MgkctSchedule.Services;

/// <summary>Просит виджет перерисоваться (после обновления данных или смены настроек).</summary>
public static class WidgetBridge
{
    public static void Refresh()
    {
#if ANDROID
        try { MgkctSchedule.Widgets.ScheduleWidgetProvider.UpdateAll(global::Android.App.Application.Context); }
        catch { /* виджет не критичен */ }
#endif
    }
}
