using System.Globalization;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using MgkctSchedule.Models;
using MgkctSchedule.Services;

// разрешения для уведомлений и восстановления будильников после перезагрузки
[assembly: UsesPermission("android.permission.POST_NOTIFICATIONS")]
[assembly: UsesPermission("android.permission.RECEIVE_BOOT_COMPLETED")]

namespace MgkctSchedule.Notifications;

/// <summary>Будильник на конец последней пары дня и уведомление "пары на завтра".</summary>
public static class TomorrowNotifier
{
    const string ChannelId = "tomorrow_schedule";
    const int NotificationId = 2001;
    public const string AlarmAction = "com.mgkct.schedule.TOMORROW";
    public const string TargetExtra = "target";

    static PendingIntent AlarmIntent(Context ctx, string? target)
    {
        var intent = new Intent(ctx, typeof(TomorrowReceiver));
        intent.SetAction(AlarmAction);
        if (target != null) intent.PutExtra(TargetExtra, target);
        return PendingIntent.GetBroadcast(ctx, 1, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    /// <summary>Пересчитать, когда слать следующее уведомление.</summary>
    public static void Reschedule(Context ctx)
    {
        var am = (AlarmManager?)ctx.GetSystemService(Context.AlarmService);
        if (am == null) return;

        if (!AppSettings.NotifyTomorrow)
        {
            am.Cancel(AlarmIntent(ctx, null));
            return;
        }

        var cache = new ScheduleService().LoadCache();
        var now = DateTime.Now;
        var plan = NotifyLogic.NextPlan(cache.Days, now, AppSettings.ShortBells, AppSettings.Subgroup, AppSettings.LastNotifiedDay);

        DateTime when;
        string? target = null;
        if (plan != null)
        {
            when = plan.FireAt > now.AddSeconds(3) ? plan.FireAt : now.AddSeconds(3);
            target = plan.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        else
        {
            // пар впереди в данных нет (например, новую неделю ещё не опубликовали): проверим позже
            when = now.AddHours(6);
        }

        long ms = new DateTimeOffset(when).ToUnixTimeMilliseconds();
        am.SetAndAllowWhileIdle(AlarmType.Rtc, ms, AlarmIntent(ctx, target));
    }

    /// <summary>Вызывается будильником: обновить данные с сайта, показать уведомление, поставить следующий будильник.</summary>
    public static async Task HandleAsync(Context ctx, string? target)
    {
        DateTime? day = null;
        if (target != null && DateTime.TryParseExact(target, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            day = d;

        // сначала помечаем день, чтобы пересчёты ниже не поставили то же уведомление повторно
        if (day != null) AppSettings.LastNotifiedDay = day;

        ScheduleCache cache;
        var svc = new ScheduleService();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            cache = await svc.RefreshAsync(cts.Token);
        }
        catch
        {
            cache = svc.LoadCache();   // нет сети: берём сохранённое
        }

        if (day != null && AppSettings.NotifyTomorrow)
        {
            var next = NotifyLogic.NextStudyDay(cache.Days, day.Value, AppSettings.Subgroup);
            if (next != null)
            {
                var text = NotifyLogic.BuildText(next.Value.Date, DateTime.Today, next.Value.Lessons);
                Post(ctx, text.Title, text.Collapsed, text.Body);
            }
        }

        Reschedule(ctx);
    }

    public static bool ShowTest(Context ctx)
    {
        var cache = new ScheduleService().LoadCache();
        var next = NotifyLogic.NextStudyDay(cache.Days, DateTime.Today, AppSettings.Subgroup);
        if (next == null)
            return Post(ctx, "Пары на завтра", "Пробное уведомление",
                        "Это пробное уведомление. В сохранённых данных нет ближайших пар.");
        var text = NotifyLogic.BuildText(next.Value.Date, DateTime.Today, next.Value.Lessons);
        return Post(ctx, text.Title, text.Collapsed, text.Body);
    }

    public static void RequestPermission()
    {
        const string perm = "android.permission.POST_NOTIFICATIONS";
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity == null || Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;
        if (activity.CheckSelfPermission(perm) != Android.Content.PM.Permission.Granted)
            activity.RequestPermissions(new[] { perm }, 1001);
    }

    static void EnsureChannel(Context ctx)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var nm = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
        if (nm == null || nm.GetNotificationChannel(ChannelId) != null) return;
        var channel = new NotificationChannel(ChannelId, "Пары на завтра", NotificationImportance.Default)
        {
            Description = "Список пар на следующий день после окончания последней пары"
        };
        nm.CreateNotificationChannel(channel);
    }

    static bool Post(Context ctx, string title, string collapsed, string body)
    {
        EnsureChannel(ctx);
        var manager = NotificationManagerCompat.From(ctx);
        if (!manager.AreNotificationsEnabled()) return false;

        var launch = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
        var builder = new NotificationCompat.Builder(ctx, ChannelId)
            .SetSmallIcon(ctx.Resources!.GetIdentifier("ic_notify", "drawable", ctx.PackageName))
            .SetContentTitle(title)
            .SetContentText(collapsed)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
            .SetAutoCancel(true);
        if (launch != null)
            builder.SetContentIntent(PendingIntent.GetActivity(ctx, 0, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable));

        manager.Notify(NotificationId, builder.Build());
        return true;
    }
}

[BroadcastReceiver(Exported = false)]
public class TomorrowReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        var target = intent?.GetStringExtra(TomorrowNotifier.TargetExtra);
        var pending = GoAsync();   // даём время на загрузку страницы
        Task.Run(async () =>
        {
            try { await TomorrowNotifier.HandleAsync(context, target); }
            catch { }
            finally { pending?.Finish(); }
        });
    }
}

/// <summary>После перезагрузки, обновления приложения или смены времени будильники сбрасываются - ставим заново.</summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[]
{
    Intent.ActionBootCompleted,
    Intent.ActionMyPackageReplaced,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged
})]
public class RestoreReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        try
        {
            TomorrowNotifier.Reschedule(context);
            MgkctSchedule.Widgets.ScheduleWidgetProvider.UpdateAll(context);
        }
        catch { }
    }
}
