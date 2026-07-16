using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LauncherRoot.Models;

namespace LauncherRoot.Views;

public partial class ModManagementView : UserControl
{
    public ModManagementView()
    {
        InitializeComponent();
    }

    private void TextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ViewModels.ModManagementViewModel vm)
        {
            vm.SearchCommand.Execute(null);
        }
    }

    private void ToggleSwitch_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && toggle.DataContext is ModInfo mod)
        {
            if (DataContext is ViewModels.ModManagementViewModel vm)
            {
                mod.Enabled = toggle.IsChecked ?? false;
                vm.ToggleItemCommand.Execute(mod);
            }
        }
    }
}
