using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Views;
using Android.Widget;
using MgkctSchedule.Services;

namespace MgkctSchedule.Widgets;

[BroadcastReceiver(Label = "Расписание", Exported = true)]
[IntentFilter(new[]
{
    AppWidgetManager.ActionAppwidgetUpdate,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged,
    Intent.ActionDateChanged
})]
[MetaData("android.appwidget.provider", Resource = "@xml/schedule_widget_info")]
public class ScheduleWidgetProvider : AppWidgetProvider
{
    const string TickAction = "com.mgkct.schedule.WIDGET_TICK";

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        base.OnUpdate(context, appWidgetManager, appWidgetIds);
        if (context != null) UpdateAll(context);
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context != null && intent != null &&
            (intent.Action == TickAction ||
             intent.Action == Intent.ActionTimeChanged ||
             intent.Action == Intent.ActionTimezoneChanged ||
             intent.Action == Intent.ActionDateChanged))
        {
            UpdateAll(context);
            return;
        }
        base.OnReceive(context, intent);
    }

    public override void OnDisabled(Context? context)
    {
        base.OnDisabled(context);
        if (context != null) Schedule(context, null);
    }

    /// <summary>Перерисовать все виджеты и поставить будильник на следующую смену пары.</summary>
    public static void UpdateAll(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null) return;
        var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(ScheduleWidgetProvider)));
        var ids = manager.GetAppWidgetIds(component);
        if (ids == null || ids.Length == 0) return;

        var cache = new ScheduleService().LoadCache();
        var model = WidgetLogic.Build(
            cache.Days,
            DateTime.Now,
            Microsoft.Maui.Storage.Preferences.Default.Get("short_bells", false),
            Microsoft.Maui.Storage.Preferences.Default.Get("subgroup", 0));

        foreach (var id in ids)
            manager.UpdateAppWidget(id, Render(context, model));

        Schedule(context, model.NextChange);
    }

    static int Res(Context c, string name, string type) =>
        c.Resources!.GetIdentifier(name, type, c.PackageName);

    static RemoteViews Render(Context ctx, WidgetModel m)
    {
        var views = new RemoteViews(ctx.PackageName, Res(ctx, "widget_schedule", "layout"));
        views.SetTextViewText(Res(ctx, "widget_header", "id"), m.Header);

        int currentBg = Res(ctx, "widget_row_current", "drawable");
        for (int i = 0; i < 3; i++)
        {
            int row = Res(ctx, $"widget_row{i + 1}", "id");
            if (i < m.Rows.Count)
            {
                var r = m.Rows[i];
                views.SetViewVisibility(row, ViewStates.Visible);
                views.SetTextViewText(Res(ctx, $"widget_row{i + 1}_title", "id"), r.Title);
                views.SetTextViewText(Res(ctx, $"widget_row{i + 1}_sub", "id"), r.Sub);
                views.SetInt(row, "setBackgroundResource", r.IsCurrent ? currentBg : 0);
            }
            else
            {
                views.SetViewVisibility(row, ViewStates.Gone);
            }
        }

        int empty = Res(ctx, "widget_empty", "id");
        views.SetTextViewText(empty, m.EmptyText);
        views.SetViewVisibility(empty, m.Rows.Count == 0 ? ViewStates.Visible : ViewStates.Gone);

        // нажатие по виджету открывает приложение
        var launch = ctx.PackageManager!.GetLaunchIntentForPackage(ctx.PackageName!);
        if (launch != null)
        {
            var pi = PendingIntent.GetActivity(ctx, 0, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            views.SetOnClickPendingIntent(Res(ctx, "widget_root", "id"), pi);
        }
        return views;
    }

    static void Schedule(Context ctx, DateTime? when)
    {
        var am = (AlarmManager?)ctx.GetSystemService(Context.AlarmService);
        if (am == null) return;

        var intent = new Intent(ctx, typeof(ScheduleWidgetProvider));
        intent.SetAction(TickAction);
        var pi = PendingIntent.GetBroadcast(ctx, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (pi == null) return;

        if (when == null) { am.Cancel(pi); return; }
        long ms = new DateTimeOffset(when.Value).ToUnixTimeMilliseconds();
        am.SetAndAllowWhileIdle(AlarmType.Rtc, ms, pi);
    }
}
