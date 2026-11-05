using System.Windows.Input;
using MgkctSchedule.Services;

namespace MgkctSchedule.ViewModels;

public class SettingsViewModel : ObservableBase
{
    string _status = "";

    public string[] Groups => ScheduleService.AvailableGroups;
    public ICommand TestNotificationCommand { get; }

    public SettingsViewModel()
    {
        TestNotificationCommand = new Command(() =>
        {
            NotificationBridge.EnsurePermission();
            Status = NotificationBridge.ShowTest()
                ? "Пробное уведомление отправлено."
                : "Уведомления запрещены. Разрешите их для приложения в настройках телефона.";
        });
    }

    public string SelectedGroup
    {
        get => AppSettings.Group;
        set
        {
            if (string.IsNullOrEmpty(value) || value == AppSettings.Group) return;
            AppSettings.Group = value;
            Raise();
            Raise(nameof(AboutText));
            AppSettings.RaiseChanged();   // главный экран загрузит расписание новой группы
        }
    }

    public bool ShortBells
    {
        get => AppSettings.ShortBells;
        set
        {
            if (value == AppSettings.ShortBells) return;
            AppSettings.ShortBells = value;
            Raise();
            AppSettings.RaiseChanged();
        }
    }

    public bool NotifyTomorrow
    {
        get => AppSettings.NotifyTomorrow;
        set
        {
            if (value == AppSettings.NotifyTomorrow) return;
            AppSettings.NotifyTomorrow = value;
            if (value) NotificationBridge.EnsurePermission();
            Raise();
            AppSettings.RaiseChanged();   // пересчитает будильник уведомления
        }
    }

    public string Status { get => _status; private set { if (Set(ref _status, value)) Raise(nameof(HasStatus)); } }
    public bool HasStatus => _status.Length > 0;

    public string AboutText
    {
        get
        {
            var cache = new ScheduleService().LoadCache();
            var upd = cache.FetchedAt == default ? "ещё не загружено" : cache.FetchedAt.ToString("dd.MM.yyyy HH:mm");
            return $"Группа {AppSettings.Group}\nДанные: mgkct.minskedu.gov.by\nОбновлено: {upd}";
        }
    }
}
