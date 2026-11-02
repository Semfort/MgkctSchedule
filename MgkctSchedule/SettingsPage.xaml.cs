using MgkctSchedule.ViewModels;

namespace MgkctSchedule;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = new SettingsViewModel();
    }

    async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();
}
