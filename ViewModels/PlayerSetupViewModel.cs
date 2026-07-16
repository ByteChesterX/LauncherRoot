using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LauncherRoot.Models;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public partial class PlayerSetupViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigService _config;
    private readonly MainWindowViewModel _main;
    private readonly ILocalizationService _localization;
    private readonly MicrosoftAuthService _microsoftAuth;
    private readonly ElybyAuthService _elybyAuth;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isMicrosoftLoggingIn;

    [ObservableProperty]
    private bool _isElybyLoggingIn;

    [ObservableProperty]
    private ObservableCollection<PlayerConfig> _accounts = [];

    [ObservableProperty]
    private PlayerConfig? _selectedAccount;

    public ILocalizationService Localization => _localization;

    public PlayerSetupViewModel(ConfigService config, MainWindowViewModel main, ILocalizationService localization)
    {
        _config = config;
        _main = main;
        _localization = localization;
        _microsoftAuth = new MicrosoftAuthService(config);
        _elybyAuth = new ElybyAuthService(config);
        _ = LoadAsync();
    }

    public void Dispose()
    {
        _microsoftAuth?.Dispose();
        _elybyAuth?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task LoadAsync()
    {
        var allAccounts = await _config.LoadAccountsAsync();
        Accounts = new ObservableCollection<PlayerConfig>(allAccounts);
        var active = await _config.LoadPlayerAsync();
        if (active != null && !string.IsNullOrEmpty(active.Username))
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == active.Id) ?? Accounts.FirstOrDefault();
        else
            SelectedAccount = Accounts.FirstOrDefault();
    }

    partial void OnSelectedAccountChanged(PlayerConfig? value)
    {
        if (value != null)
            _ = _config.SwitchAccountAsync(value.Id);
    }

    [RelayCommand]
    private async Task Continue()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = _localization["error.noplayer"];
            return;
        }

        var trimmed = Username.Trim();
        if (Accounts.Any(a => a.Username == trimmed))
        {
            ErrorMessage = _localization["error.duplicate_user"];
            return;
        }

        var player = new PlayerConfig { Username = trimmed };
        await _config.SavePlayerAsync(player);
        _config.Log($"Kullanıcı kaydedildi: {Username}");

        Accounts.Add(player);
        SelectedAccount = player;

        _main.NavigateTo(PageType.MainMenu);
    }

    [RelayCommand]
    private async Task MicrosoftLogin()
    {
        IsMicrosoftLoggingIn = true;
        ErrorMessage = "";

        var cfg = await _config.LoadConfigAsync();
        if (!string.IsNullOrWhiteSpace(cfg.MicrosoftClientId))
            _microsoftAuth.SetClientId(cfg.MicrosoftClientId);

        var result = await _microsoftAuth.LoginWithBrowserAsync(
            _ => Task.CompletedTask);

        IsMicrosoftLoggingIn = false;

        if (result.Success)
        {
            if (Accounts.Any(a => a.Username == result.Username))
            {
                ErrorMessage = $"{_localization["error.duplicate_user"]}: {result.Username}";
                return;
            }

            var player = new PlayerConfig
            {
                Username = result.Username,
                Uuid = result.Uuid,
                AccessToken = result.AccessToken,
                AuthType = result.AuthType
            };
            await _config.SavePlayerAsync(player);
            Accounts.Add(player);
            SelectedAccount = player;
            _main.NavigateTo(PageType.MainMenu);
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? _localization["error.microsoft_failed"];
        }
    }

    [RelayCommand]
    private async Task SelectAccount(PlayerConfig? account)
    {
        if (account == null) return;
        await _config.SwitchAccountAsync(account.Id);
        _main.NavigateTo(PageType.MainMenu);
    }

    [RelayCommand]
    private async Task ElybyLogin()
    {
        var cfg = await _config.LoadConfigAsync();
        if (!string.IsNullOrWhiteSpace(cfg.ElybyClientId))
            _elybyAuth.SetClientId(cfg.ElybyClientId);

        IsElybyLoggingIn = true;
        ErrorMessage = "";

        var result = await _elybyAuth.LoginWithBrowserAsync();

        IsElybyLoggingIn = false;

        if (result.Success)
        {
            if (Accounts.Any(a => a.Username == result.Username))
            {
                ErrorMessage = $"{_localization["error.duplicate_user"]}: {result.Username}";
                return;
            }

            var player = new PlayerConfig
            {
                Username = result.Username,
                Uuid = result.Uuid,
                AccessToken = result.AccessToken,
                AuthType = result.AuthType
            };
            await _config.SavePlayerAsync(player);
            Accounts.Add(player);
            SelectedAccount = player;
            _main.NavigateTo(PageType.MainMenu);
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? _localization["error.elyby_failed"];
        }
    }

    [RelayCommand]
    private async Task DeleteAccount(PlayerConfig? account)
    {
        if (account == null) return;
        await _config.DeleteAccountAsync(account.Id);
        Accounts.Remove(account);
        if (SelectedAccount == account)
            SelectedAccount = Accounts.FirstOrDefault();
    }
}
