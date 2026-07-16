using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LauncherRoot.Services;
using System;
using System.IO;

namespace LauncherRoot.Views;

public partial class ScreenshotGalleryWindow : Window
{
    private readonly string[] _files;
    private readonly ILocalizationService _localization;

    public ScreenshotGalleryWindow()
    {
        InitializeComponent();
        _files = [];
        _localization = null!;
    }

    public ScreenshotGalleryWindow(string[] files, ILocalizationService localization)
    {
        _files = files;
        _localization = localization;
        InitializeComponent();
        Title = _localization["screenshots.title"];
        LoadScreenshots();
    }

    private IBrush? Res(string key)
    {
        return Application.Current?.FindResource(key) as IBrush;
    }

    private void LoadScreenshots()
    {
        var borderBrush = Res("BorderBrush");
        var bgBrush = Res("BgCardBrush");
        var mutedBrush = Res("TextMutedBrush");

        foreach (var file in _files)
        {
            try
            {
                using var stream = File.OpenRead(file);
                var bitmap = new Bitmap(stream);
                var info = new FileInfo(file);

                var border = new Border
                {
                    Width = 200,
                    Height = 150,
                    Margin = new Avalonia.Thickness(4),
                    CornerRadius = new Avalonia.CornerRadius(8),
                    ClipToBounds = true,
                    BorderThickness = new Avalonia.Thickness(1),
                    BorderBrush = borderBrush,
                    Background = bgBrush,
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                };

                var stack = new StackPanel();

                var image = new Image
                {
                    Source = bitmap,
                    Stretch = Avalonia.Media.Stretch.UniformToFill,
                    Width = 200,
                    Height = 130,
                };

                border.Tapped += (_, _) =>
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = file,
                        UseShellExecute = true,
                    };
                    System.Diagnostics.Process.Start(psi);
                };

                var dateText = new TextBlock
                {
                    Text = info.CreationTime.ToString("g"),
                    FontSize = 10,
                    Margin = new Avalonia.Thickness(4, 2),
                    Foreground = mutedBrush,
                };

                stack.Children.Add(image);
                stack.Children.Add(dateText);
                border.Child = stack;
                GalleryPanel.Children.Add(border);
            }
            catch { }
        }
    }
}
