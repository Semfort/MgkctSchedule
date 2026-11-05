using System.ComponentModel;
using System.Runtime.CompilerServices;
using MgkctSchedule.Models;

namespace MgkctSchedule.ViewModels;

public class ObservableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

public enum PairState { Past, Current, Next, Future }

/// <summary>Кнопка дня в полосе Пн-Сб.</summary>
public class DayChip
{
    static readonly string[] Names = { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };

    public DateTime Date { get; }
    public bool HasData { get; }
    public bool IsSelected { get; }
    public bool IsToday { get; }

    public DayChip(DateTime date, bool hasData, bool isSelected, bool isToday)
    {
        Date = date; HasData = hasData; IsSelected = isSelected; IsToday = isToday;
    }

    public string DayName => Names[(int)Date.DayOfWeek];
    public string DayNumber => Date.Day.ToString();
    public Color Background => IsSelected ? Palette.ChipSelected : Palette.Chip;
    public Color TextColor => IsSelected ? Colors.White : Palette.ChipText;
    public Color DotColor => IsSelected ? Colors.White : Palette.AccentCurrent;
    public double ChipOpacity => HasData || IsSelected ? 1.0 : 0.45;
}

public class TeacherLink
{
    public string Name { get; init; } = "";
}

/// <summary>Одно занятие в карточке.</summary>
public class EntryVm
{
    public string Subject { get; init; } = "";
    public string TypeLine { get; init; } = "";
    public List<TeacherLink> Teachers { get; init; } = new();
    public string InfoLine { get; init; } = "";
    public string Room { get; init; } = "";
    public bool HasTeachers => Teachers.Count > 0;
    public bool HasInfo => InfoLine.Length > 0;
    public bool HasRoom => Room.Length > 0;
    public Color TitleColor => Palette.Title;
    public Color SubColor => Palette.Sub;
    public Color RoomChipColor => Palette.RoomChip;

    static string TypeName(string t) => t switch
    {
        "Лек" => "Лекция",
        "ЛР" => "Лабораторная",
        "Пр-ка" => "Практика",
        "ф-в" => "Факультатив",
        "КП" => "Курсовой проект",
        _ => t
    };

    public static EntryVm From(LessonEntry e, bool showSubgroup, bool teacherMode = false)
    {
        var parts = new List<string>();
        if (e.Type.Length > 0) parts.Add(TypeName(e.Type));
        if (showSubgroup && e.Subgroup != null) parts.Add($"подгруппа {e.Subgroup}");
        return new EntryVm
        {
            Subject = e.Subject,
            TypeLine = string.Join(" · ", parts),
            // в расписании группы - кликабельные преподаватели, в расписании преподавателя - группа
            Teachers = teacherMode
                ? new List<TeacherLink>()
                : e.Teacher.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => new TeacherLink { Name = t }).ToList(),
            InfoLine = teacherMode && e.Group.Length > 0 ? $"Группа {e.Group}" : "",
            Room = e.Room
        };
    }
}

/// <summary>Элемент списка: пара либо "окно" между парами.</summary>
public class PairItem : ObservableBase
{
    PairState _state = PairState.Future;
    double _progress;

    public int Number { get; }
    public BellSlot? Slot { get; }
    public DateTime? Start { get; }
    public DateTime? End { get; }
    public bool IsWindow { get; }
    public List<EntryVm> Entries { get; }

    public PairItem(DateTime date, int number, BellSlot? slot, List<EntryVm> entries, bool isWindow)
    {
        Number = number; Slot = slot; Entries = entries; IsWindow = isWindow;
        if (slot != null) { Start = date + slot.Start; End = date + slot.End; }
    }

    public bool IsLesson => !IsWindow;
    public string NumberText => $"{Number} пара";
    public string StartText => Slot == null ? "" : Slot.Start.ToString(@"hh\:mm");
    public string EndText => Slot == null ? "" : Slot.End.ToString(@"hh\:mm");

    public void Apply(PairState state, double progress)
    {
        _state = state;
        _progress = progress;
        Raise(string.Empty); // обновить все привязки
    }

    public bool IsCurrent => _state == PairState.Current;
    public double Progress => _progress;
    public bool HasBadge => _state is PairState.Current or PairState.Next;
    public string BadgeText => _state == PairState.Current ? "СЕЙЧАС" : _state == PairState.Next ? "СЛЕДУЮЩАЯ" : "";

    public Color CardColor => _state switch
    {
        PairState.Current => Palette.CardCurrent,
        PairState.Next => Palette.CardNext,
        _ => Palette.Card
    };

    public Color AccentColor => _state switch
    {
        PairState.Current => Palette.AccentCurrent,
        PairState.Next => Palette.AccentNext,
        _ => Palette.Bar
    };

    public double CardOpacity => _state == PairState.Past ? 0.55 : 1.0;
    public Color TitleColor => Palette.Title;
    public Color SubColor => Palette.Sub;
}
