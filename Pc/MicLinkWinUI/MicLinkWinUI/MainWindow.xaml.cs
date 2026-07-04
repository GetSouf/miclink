using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Core.DependencyInjection;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Infrastructure.Theming;
using MicLinkWinUI.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MicLinkWinUI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public EffectsChainViewModel EffectsChainViewModel { get; }
    public LogsViewModel LogsViewModel { get; }

    public string MicDeviceName => AppConstants.VirtualMicName;
    public string CameraDeviceName => AppConstants.VirtualCameraName;

    private EffectSlotViewModel? _dragSlot;
    private bool _deleteZoneHover;

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

        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.IsMicrophoneMuted))
            {
                UpdateMicMuteIcon();
            }
        };

        MainNav.SelectedItem = MainNav.MenuItems[0];
        ShowSection("mic");
        UpdateMicMuteIcon();
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

    private async void MicMuteButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleMicrophoneMuteCommand.ExecuteAsync(null);
        UpdateMicMuteIcon();
    }

    private void MicSpectrum_ToggleRequested(object sender, EventArgs e)
    {
        ViewModel.ToggleSpectrumVisualizerCommand.Execute(null);
    }

    private void UpdateMicMuteIcon()
    {
        MicMuteIcon.Glyph = ViewModel.IsMicrophoneMuted ? "\uE1D6" : "\uE720";
        MicMuteButton.IsChecked = ViewModel.IsMicrophoneMuted;
    }

    private void ChainList_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        if (args.Items.FirstOrDefault() is EffectSlotViewModel slot)
        {
            _dragSlot = slot;
            SetDeleteZoneHover(false);
        }
    }

    private void ChainList_DragItemsCompleted(object sender, DragItemsCompletedEventArgs args)
    {
        if (_deleteZoneHover && _dragSlot is not null)
        {
            EffectsChainViewModel.RemoveSlot(_dragSlot);
        }
        else if (_dragSlot is not null && EffectsChainViewModel.Chain.Contains(_dragSlot))
        {
            EffectsChainViewModel.OnChainReordered();
        }

        EndDrag();
    }

    private void FxDeleteZone_DragOver(object sender, DragEventArgs e)
    {
        if (_dragSlot is null)
        {
            return;
        }

        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.DragUIOverride.Caption = "Удалить";
        SetDeleteZoneHover(true);
    }

    private void FxDeleteZone_DragLeave(object sender, DragEventArgs e)
    {
        SetDeleteZoneHover(false);
    }

    private void FxDeleteZone_Drop(object sender, DragEventArgs e)
    {
        if (_dragSlot is not null)
        {
            EffectsChainViewModel.RemoveSlot(_dragSlot);
        }

        e.Handled = true;
        EndDrag();
    }

    private void SetDeleteZoneHover(bool hover)
    {
        if (_deleteZoneHover == hover)
        {
            return;
        }

        _deleteZoneHover = hover;
        FxDeleteZone.Opacity = hover ? 0.42 : 1;
        FxDeleteZone.BorderBrush = new SolidColorBrush(
            hover ? Windows.UI.Color.FromArgb(255, 231, 76, 60)
                  : Windows.UI.Color.FromArgb(85, 231, 76, 60));
    }

    private void EndDrag()
    {
        _dragSlot = null;
        SetDeleteZoneHover(false);
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
