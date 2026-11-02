namespace MgkctSchedule.ViewModels;

/// <summary>Цвета с учётом светлой/тёмной темы (читаются в момент построения элементов).</summary>
public static class Palette
{
    public static bool Dark => Application.Current?.RequestedTheme == AppTheme.Dark;

    static Color Pick(string light, string dark) => Color.FromArgb(Dark ? dark : light);

    public static Color Card => Pick("#FFFFFF", "#1B1E25");
    public static Color CardCurrent => Pick("#DCE8FF", "#17325C");
    public static Color CardNext => Pick("#FFF1D6", "#3A2E14");
    public static Color Title => Pick("#14171C", "#F1F3F7");
    public static Color Sub => Pick("#5B6472", "#9AA3B2");
    public static Color Bar => Pick("#C9D0DC", "#2E343F");
    public static Color AccentCurrent => Pick("#2F6BFF", "#6B96FF");
    public static Color AccentNext => Pick("#F59E0B", "#FBBF24");
    public static Color Chip => Pick("#E6EAF2", "#1B1E25");
    public static Color ChipSelected => Pick("#2F6BFF", "#4C7DFF");
    public static Color ChipText => Pick("#14171C", "#F1F3F7");
    public static Color RoomChip => Pick("#EEF1F7", "#2A2F3A");
    public static Color Overlay => Color.FromArgb("#99000000");
}
