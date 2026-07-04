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
    public EffectsChainViewModel EffectsChainViewModel { get; }
    public LogsViewModel LogsViewModel { get; }

    public string MicDeviceName => AppConstants.VirtualMicName;
    public string CameraDeviceName => AppConstants.VirtualCameraName;

    public MainWindow()
    {
        ViewModel = AppServices.GetRequired<MainViewModel>();
        SettingsViewModel = AppServices.GetRequired<SettingsViewModel>();
        EffectsChainViewModel = AppServices.GetRequired<EffectsChainViewModel>();
        LogsViewModel = AppServices.GetRequired<LogsViewModel>();

        InitializeComponent();
        Title = AppConstants.AppName;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));

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

        MainNav.SelectedItem = MainNav.MenuItems[0];
        ShowSection("mic");
    }

    private void MainNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            ShowSection(tag);
        }
    }

    private void ShowSection(string tag)
    {
        SectionMic.Visibility = tag == "mic" ? Visibility.Visible : Visibility.Collapsed;
        SectionFx.Visibility = tag == "fx" ? Visibility.Visible : Visibility.Collapsed;
        SectionCam.Visibility = tag == "cam" ? Visibility.Visible : Visibility.Collapsed;
        SectionSettings.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        SectionLogs.Visibility = tag == "logs" ? Visibility.Visible : Visibility.Collapsed;
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

    private void BuiltInLibrary_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EffectLibraryItemViewModel item)
        {
            EffectsChainViewModel.AddBuiltInCommand.Execute(item);
        }
    }

    private void VstLibrary_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EffectLibraryItemViewModel item)
        {
            EffectsChainViewModel.AddVstCommand.Execute(item);
        }
    }

    private void ChainList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        EffectsChainViewModel.OnChainReordered();
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
