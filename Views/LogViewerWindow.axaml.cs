using Avalonia.Controls;
using Avalonia.Interactivity;
using LauncherRoot.Services;
using System;
using System.IO;
using System.Linq;

namespace LauncherRoot.Views;

public partial class LogViewerWindow : Window
{
    private readonly IConfigService _config;
    private readonly ILocalizationService _localization;

#pragma warning disable CS8618
    public LogViewerWindow()
    {
        InitializeComponent();
    }
#pragma warning restore CS8618

    public LogViewerWindow(ILocalizationService localization)
    {
        InitializeComponent();
        _config = new ConfigService();
        _localization = localization;
        Title = _localization["logviewer.title"];
        HeaderText.Text = _localization["logviewer.title"];
        RefreshText.Text = _localization["logviewer.refresh"];
        LoadLogContent();
    }

    private void LoadLogContent()
    {
        try
        {
            var logsDir = _config.GetLogsPath();
            if (!Directory.Exists(logsDir))
            {
                LogContent.Text = _localization["logviewer.nologs"];
                FooterText.Text = logsDir;
                return;
            }

            var logPath = Directory.GetFiles(logsDir, "launcher-*.log")
                                   .OrderByDescending(f => f)
                                   .FirstOrDefault();

            if (logPath == null)
            {
                LogContent.Text = _localization["logviewer.nologs"];
                FooterText.Text = logsDir;
                return;
            }

            var content = File.ReadAllText(logPath);
            LogContent.Text = string.IsNullOrWhiteSpace(content)
                ? _localization["logviewer.empty"]
                : content;
            FooterText.Text = logPath;
        }
        catch (Exception ex)
        {
            LogContent.Text = $"{_localization["logviewer.error"]}: {ex.Message}";
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        LoadLogContent();
    }
}
