using System.Collections.ObjectModel;
using System.Windows.Input;
using MgkctSchedule.Models;
using MgkctSchedule.Services;

namespace MgkctSchedule.ViewModels;

public class MainViewModel : ObservableBase
{
    static readonly string[] DayNamesFull =
        { "Воскресенье", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };
    static readonly string[] MonthsGen =
        { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };

    ScheduleService _service = new();
    string _group = AppSettings.Group;
    readonly Dictionary<DateTime, DaySchedule> _days = new();

    DateTime _selected = NormalizeDate(DateTime.Today);
    DateTime _fetchedAt;
    string _error = "";
    string _status = "";
    string _emptyText = "";
    bool _isRefreshing;
    bool _shortBells = AppSettings.ShortBells;
    int _subgroupIndex = AppSettings.Subgroup;
    DateTime _lastTickDate = DateTime.Today;

    public ObservableCollection<DayChip> WeekDays { get; } = new();
    public ObservableCollection<PairItem> Items { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand PrevDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand PrevWeekCommand { get; }
    public ICommand NextWeekCommand { get; }

    public MainViewModel()
    {
        RefreshCommand = new Command(async () => await RefreshAsync());
        TodayCommand = new Command(() => SelectDate(DateTime.Today));
        PrevDayCommand = new Command(() => SelectDate(_selected.AddDays(-1)));
        NextDayCommand = new Command(() => SelectDate(_selected.AddDays(1)));
        PrevWeekCommand = new Command(() => SelectDate(_selected.AddDays(-7)));
        NextWeekCommand = new Command(() => SelectDate(_selected.AddDays(7)));
        AppSettings.Changed += OnSettingsChanged;
    }

    // ---------- свойства ----------

    public DateTime SelectedDate => _selected;
    public string DateTitle => $"{DayNamesFull[(int)_selected.DayOfWeek]}, {_selected.Day} {MonthsGen[_selected.Month - 1]}";

    public string SubTitle
    {
        get
        {
            var monday = MondayOf(_selected);
            var upd = _fetchedAt == default ? "ещё не загружено" : $"обновлено {_fetchedAt:dd.MM HH:mm}";
            return $"Группа {_group} · {monday:dd.MM}–{monday.AddDays(5):dd.MM} · {upd}";
        }
    }

    public string ErrorText => _error;
    public bool HasError => _error.Length > 0;

    public string StatusText { get => _status; private set { if (Set(ref _status, value)) Raise(nameof(HasStatus)); } }
    public bool HasStatus => _status.Length > 0;
    public string EmptyText { get => _emptyText; private set => Set(ref _emptyText, value); }

    public bool IsRefreshing { get => _isRefreshing; set => Set(ref _isRefreshing, value); }

    void OnSettingsChanged()
    {
        if (_group != AppSettings.Group)
        {
            _ = SwitchGroupAsync();   // другая группа: загружаем и разбираем только её
            return;
        }
        _shortBells = AppSettings.ShortBells;
        _subgroupIndex = AppSettings.Subgroup;
        BuildItems();
        UpdateStates();
    }

    public bool HasData(DateTime date) => _days.ContainsKey(date.Date);

    // ---------- загрузка ----------

    public async Task InitializeAsync()
    {
        var cache = _service.LoadCache();
        foreach (var d in cache.Days) _days[d.Date.Date] = d;
        _fetchedAt = cache.FetchedAt;
        SelectDate(DateTime.Today);
        await RefreshAsync();
    }

    async Task SwitchGroupAsync()
    {
        _group = AppSettings.Group;
        _service = new ScheduleService(_group);
        _days.Clear();
        _error = "";
        var cache = _service.LoadCache();               // если эту группу уже открывали, покажем сохранённое
        foreach (var d in cache.Days) _days[d.Date.Date] = d;
        _fetchedAt = cache.FetchedAt;
        Raise(nameof(ErrorText)); Raise(nameof(HasError));
        SelectDate(DateTime.Today);
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        var svc = _service;
        var group = _group;
        try
        {
            var cache = await svc.RefreshAsync();
            if (group != _group) return;                // за время загрузки выбрали другую группу
            _days.Clear();
            foreach (var d in cache.Days) _days[d.Date.Date] = d;
            _fetchedAt = cache.FetchedAt;
            _error = "";
        }
        catch (InvalidDataException ex)
        {
            _error = "Не удалось разобрать страницу: " + ex.Message;
        }
        catch (Exception)
        {
            _error = "Нет связи с сайтом — показаны сохранённые данные";
        }
        finally
        {
            IsRefreshing = false;
        }
        Raise(nameof(ErrorText)); Raise(nameof(HasError)); Raise(nameof(SubTitle));
        BuildWeek();
        BuildItems();
        UpdateStates();
    }

    // ---------- навигация ----------

    static DateTime NormalizeDate(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? d.Date.AddDays(1) : d.Date;
    static DateTime MondayOf(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public void SelectDate(DateTime date)
    {
        _selected = NormalizeDate(date);
        Raise(nameof(SelectedDate)); Raise(nameof(DateTitle)); Raise(nameof(SubTitle));
        BuildWeek();
        BuildItems();
        UpdateStates();
    }

    void BuildWeek()
    {
        WeekDays.Clear();
        var monday = MondayOf(_selected);
        for (int i = 0; i < 6; i++)
        {
            var d = monday.AddDays(i);
            WeekDays.Add(new DayChip(d, HasData(d), d == _selected, d == DateTime.Today));
        }
    }

    /// <summary>Вызывается при смене системной темы.</summary>
    public void RebuildForTheme()
    {
        BuildWeek();
        BuildItems();
        UpdateStates();
    }

    // ---------- список пар ----------

    List<LessonEntry> FilterBySubgroup(List<LessonEntry> entries) =>
        _subgroupIndex == 0
            ? entries
            : entries.Where(e => e.Subgroup == null || e.Subgroup == _subgroupIndex).ToList();

    void BuildItems()
    {
        Items.Clear();

        if (!_days.TryGetValue(_selected, out var day))
        {
            EmptyText = "Нет данных на эту дату.\nПотяните вниз, чтобы обновить.";
            return;
        }

        var bells = Bells.For(_selected.DayOfWeek, _shortBells);
        var rows = day.Pairs.OrderBy(p => p.Number)
            .Select(p => (p.Number, Entries: FilterBySubgroup(p.Entries)))
            .ToList();

        int first = rows.FindIndex(r => r.Entries.Count > 0);
        int last = rows.FindLastIndex(r => r.Entries.Count > 0);
        if (first < 0)
        {
            EmptyText = "Занятий нет 🎉";
            return;
        }

        for (int i = first; i <= last; i++)
        {
            var (number, entries) = rows[i];
            var slot = bells.FirstOrDefault(b => b.Number == number);
            bool isWindow = entries.Count == 0;
            var vms = entries.Select(e => EntryVm.From(e, showSubgroup: _subgroupIndex == 0)).ToList();
            Items.Add(new PairItem(_selected, number, slot, vms, isWindow));
        }
    }

    // ---------- подсветка по времени ----------

    public void Tick()
    {
        if (_lastTickDate != DateTime.Today)
        {
            // наступил новый день, пока приложение было открыто
            _lastTickDate = DateTime.Today;
            BuildWeek();
        }
        UpdateStates();
    }

    static string Fmt(TimeSpan ts)
    {
        int m = (int)Math.Ceiling(ts.TotalMinutes);
        if (m < 1) m = 1;
        return m >= 60 ? $"{m / 60} ч {m % 60:00} мин" : $"{m} мин";
    }

    void UpdateStates()
    {
        var now = DateTime.Now;
        var lessons = Items.Where(i => i.IsLesson && i.Start != null).ToList();

        if (_selected < now.Date)
        {
            foreach (var it in Items) it.Apply(PairState.Past, 0);
            StatusText = "";
            return;
        }
        if (_selected > now.Date)
        {
            foreach (var it in Items) it.Apply(PairState.Future, 0);
            StatusText = "";
            return;
        }

        var current = lessons.FirstOrDefault(i => i.Start <= now && now < i.End);
        var next = lessons.FirstOrDefault(i => i.Start > now);

        foreach (var it in Items)
        {
            if (it.End != null && it.End <= now) it.Apply(PairState.Past, 1);
            else if (it == current)
            {
                double p = (now - it.Start!.Value).TotalSeconds / (it.End!.Value - it.Start.Value).TotalSeconds;
                it.Apply(PairState.Current, Math.Clamp(p, 0, 1));
            }
            else if (it == next) it.Apply(PairState.Next, 0);
            else it.Apply(PairState.Future, 0);
        }

        if (current != null)
        {
            var s = current.Slot!;
            if (s.BreakStart != null && s.BreakEnd != null &&
                now >= now.Date + s.BreakStart && now < now.Date + s.BreakEnd)
                StatusText = $"{current.Number}-я пара · перерыв до {s.BreakEnd.Value.ToString(@"hh\:mm")}";
            else
                StatusText = $"Идёт {current.Number}-я пара · до конца {Fmt(current.End!.Value - now)}";
        }
        else if (next != null)
        {
            var left = Fmt(next.Start!.Value - now);
            StatusText = next == lessons[0]
                ? $"Пары начнутся в {next.StartText} · через {left}"
                : $"Перемена · {next.Number}-я пара в {next.StartText} (через {left})";
        }
        else
        {
            StatusText = lessons.Count > 0 ? "Пары на сегодня закончились" : "";
        }
    }
}
