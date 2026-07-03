using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Core.DependencyInjection;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Infrastructure.Theming;
using MicLinkWinUI.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MicLinkWinUI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public LogsViewModel LogsViewModel { get; }

    public string MicDeviceName => AppConstants.VirtualMicName;
    public string CameraDeviceName => AppConstants.VirtualCameraName;

    public MainWindow()
    {
        ViewModel = AppServices.GetRequired<MainViewModel>();
        SettingsViewModel = AppServices.GetRequired<SettingsViewModel>();
        LogsViewModel = AppServices.GetRequired<LogsViewModel>();

        InitializeComponent();
        Title = AppConstants.AppName;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 720));

        var themeService = AppServices.GetRequired<IThemeService>();
        themeService.ThemeChanged += OnThemeChanged;
        ApplyTheme(themeService.Current);

        ThemeRadioButtons.SelectedIndex = SettingsViewModel.SelectedTheme switch
        {
            AppThemeMode.System => 0,
            AppThemeMode.Dark => 1,
            AppThemeMode.Light => 2,
            _ => 1
        };
    }

    private void ThemeRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not RadioButtons radioButtons)
        {
            return;
        }

        if (radioButtons.SelectedItem is not RadioButton selected)
        {
            return;
        }

        if (selected.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<AppThemeMode>(tag, out var mode))
        {
            return;
        }

        SettingsViewModel.SelectedTheme = mode;
    }

    private void AccentColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string hex)
        {
            return;
        }

        SettingsViewModel.SelectedAccent = hex;
    }

    private void OnThemeChanged(object? sender, Domain.Models.ThemeSettings settings)
    {
        ApplyTheme(settings);
    }

    private void ApplyTheme(Domain.Models.ThemeSettings settings)
    {
        if (Content is FrameworkElement root)
        {
            ThemeApplicator.Apply(settings, root);
        }
        else
        {
            ThemeApplicator.Apply(settings);
        }
    }

    public async Task PromptDriverSetupIfNeededAsync()
    {
        var bootstrap = AppServices.GetRequired<IAppBootstrapService>();
        if (!bootstrap.NeedsMicrophoneDriverSetup)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Установка микрофона MicLink",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.WrapWholeWords,
                Text =
                    "Для Discord и других программ нужен драйвер виртуального микрофона.\n\n" +
                    "Нажмите «Установить» — Windows запросит права администратора (один раз).\n\n" +
                    "После установки выберите в Discord: «MicLink Virtual Audio» / «Микрофон (MicLink Virtual Audio)».",
            },
            PrimaryButtonText = "Установить",
            CloseButtonText = "Позже",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await SettingsViewModel.InstallDriverCommand.ExecuteAsync(null);
    }
}
