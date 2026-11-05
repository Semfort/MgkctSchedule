using MgkctSchedule.Services;
using MgkctSchedule.ViewModels;

namespace MgkctSchedule;

public partial class MainPage : ContentPage
{
    static readonly string[] MonthsNom =
        { "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь" };
    static readonly string[] WeekdayShort = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

    readonly MainViewModel _vm = new();
    IDispatcherTimer? _timer;
    DateTime _calMonth = DateTime.Today;
    bool _started;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Application.Current != null)
            Application.Current.RequestedThemeChanged += OnThemeChanged;

        // раз в 20 секунд пересчитываем, какая пара идёт
        if (_timer == null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(20);
            _timer.Tick += (_, _) => _vm.Tick();
        }
        _timer.Start();
        _vm.Tick();

        if (!_started)
        {
            _started = true;
            await _vm.InitializeAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        if (Application.Current != null)
            Application.Current.RequestedThemeChanged -= OnThemeChanged;
    }

    void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => _vm.RebuildForTheme();

    async void OnSettingsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new SettingsPage());

    // ---------- преподаватели ----------

    async void OnTeacherTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is TeacherLink t)
            await _vm.OpenTeacherAsync(t.Name);
    }

    void OnBackToGroupTapped(object? sender, TappedEventArgs e) => _vm.ExitTeacherMode();

    /// <summary>Кнопка "назад" на телефоне: из режима преподавателя к группе, а не из приложения.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsTeacherMode) { _vm.ExitTeacherMode(); return true; }
        return base.OnBackButtonPressed();
    }

    // ---------- подгруппа ----------

    bool _switchingSubgroup;

    /// <summary>Все -> Первая -> Вторая -> Все. Подпись и список "пролистываются" влево.</summary>
    async void OnSubgroupTapped(object? sender, TappedEventArgs e)
    {
        if (_switchingSubgroup) return;
        _switchingSubgroup = true;
        try
        {
            await Task.WhenAll(
                SubgroupLabel.TranslateTo(-28, 0, 110, Easing.CubicIn),
                SubgroupLabel.FadeTo(0, 110),
                ListHost.TranslateTo(-48, 0, 110, Easing.CubicIn),
                ListHost.FadeTo(0, 110));

            AppSettings.Subgroup = (AppSettings.Subgroup + 1) % 3;
            AppSettings.RaiseChanged();   // обновит подпись, список, виджет и уведомление

            SubgroupLabel.TranslationX = 28;
            ListHost.TranslationX = 48;
            await Task.WhenAll(
                SubgroupLabel.TranslateTo(0, 0, 160, Easing.CubicOut),
                SubgroupLabel.FadeTo(1, 160),
                ListHost.TranslateTo(0, 0, 180, Easing.CubicOut),
                ListHost.FadeTo(1, 180));
        }
        finally
        {
            SubgroupLabel.TranslationX = 0; SubgroupLabel.Opacity = 1;
            ListHost.TranslationX = 0; ListHost.Opacity = 1;
            _switchingSubgroup = false;
        }
    }

    // ---------- дни ----------

    void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is DayChip chip)
            _vm.SelectDate(chip.Date);
    }

    void OnSwipeLeft(object? sender, SwipedEventArgs e) => _vm.NextDayCommand.Execute(null);
    void OnSwipeRight(object? sender, SwipedEventArgs e) => _vm.PrevDayCommand.Execute(null);

    // ---------- календарик ----------

    void OnCalendarClicked(object? sender, EventArgs e)
    {
        _calMonth = new DateTime(_vm.SelectedDate.Year, _vm.SelectedDate.Month, 1);
        BuildCalendar();
        CalendarOverlay.IsVisible = true;
    }

    void OnCalendarClose(object? sender, EventArgs e) => CalendarOverlay.IsVisible = false;
    void OnCalendarSwallowTap(object? sender, TappedEventArgs e) { }

    void OnCalPrev(object? sender, EventArgs e) { _calMonth = _calMonth.AddMonths(-1); BuildCalendar(); }
    void OnCalNext(object? sender, EventArgs e) { _calMonth = _calMonth.AddMonths(1); BuildCalendar(); }

    void OnCalToday(object? sender, EventArgs e)
    {
        _vm.SelectDate(DateTime.Today);
        CalendarOverlay.IsVisible = false;
    }

    void BuildCalendar()
    {
        CalendarTitle.Text = $"{MonthsNom[_calMonth.Month - 1]} {_calMonth.Year}";

        if (CalendarWeekdays.Children.Count == 0)
        {
            for (int i = 0; i < 7; i++)
            {
                var l = new Label
                {
                    Text = WeekdayShort[i],
                    FontSize = 12,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Palette.Sub
                };
                CalendarWeekdays.Add(l, i, 0);
            }
        }

        CalendarGrid.Children.Clear();
        int offset = ((int)_calMonth.DayOfWeek + 6) % 7;  // понедельник = 0
        int days = DateTime.DaysInMonth(_calMonth.Year, _calMonth.Month);

        for (int day = 1; day <= days; day++)
        {
            var date = new DateTime(_calMonth.Year, _calMonth.Month, day);
            int cell = offset + day - 1;
            bool selected = date == _vm.SelectedDate;
            bool today = date == DateTime.Today;
            bool hasData = _vm.HasData(date);

            var label = new Label
            {
                Text = day.ToString(),
                FontSize = 15,
                FontAttributes = selected || today ? FontAttributes.Bold : FontAttributes.None,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TextColor = selected ? Colors.White : Palette.Title
            };

            var circle = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                StrokeThickness = today && !selected ? 2 : 0,
                Stroke = Palette.AccentCurrent,
                BackgroundColor = selected ? Palette.ChipSelected : Colors.Transparent,
                Opacity = hasData || selected ? 1.0 : 0.45,
                Content = label
            };

            var captured = date;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _vm.SelectDate(captured);
                CalendarOverlay.IsVisible = false;
            };
            circle.GestureRecognizers.Add(tap);

            CalendarGrid.Add(circle, cell % 7, cell / 7);
        }
    }
}
