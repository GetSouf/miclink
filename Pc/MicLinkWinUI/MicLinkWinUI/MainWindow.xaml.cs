using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Core.DependencyInjection;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Infrastructure.Theming;
using MicLinkWinUI.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

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
    private Border? _activeDropLine;

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

    private void ChainSlot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is EffectSlotViewModel slot)
        {
            EffectsChainViewModel.SelectedSlot = slot;
        }
    }

    private void ChainSlot_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not FrameworkElement element || element.DataContext is not EffectSlotViewModel slot)
        {
            return;
        }

        _dragSlot = slot;
        args.Data.SetText(slot.SlotId);
        args.DragUI.SetContentFromDataPackage();
        FxTrashOverlay.Visibility = Visibility.Visible;
    }

    private void ChainSlot_DragOver(object sender, DragEventArgs e)
    {
        if (_dragSlot is null)
        {
            return;
        }

        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.DragUIOverride.Caption = "Переместить";

        if (sender is not FrameworkElement element || element.DataContext is not EffectSlotViewModel target)
        {
            return;
        }

        var index = EffectsChainViewModel.Chain.IndexOf(target);
        var insertAbove = e.GetPosition(element).Y < element.ActualHeight / 2;
        ShowDropIndicator(insertAbove ? index : index + 1);
    }

    private void ChainSlot_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.FindName("DropLine") is Border line &&
            ReferenceEquals(_activeDropLine, line))
        {
            HideDropIndicator();
        }
    }

    private void ChainSlot_Drop(object sender, DragEventArgs e)
    {
        if (_dragSlot is null)
        {
            EndDrag();
            return;
        }

        if (sender is FrameworkElement element && element.DataContext is EffectSlotViewModel target)
        {
            var index = EffectsChainViewModel.Chain.IndexOf(target);
            var insertAt = e.GetPosition(element).Y < element.ActualHeight / 2 ? index : index + 1;
            EffectsChainViewModel.MoveSlot(_dragSlot, insertAt);
        }

        EndDrag();
        e.Handled = true;
    }

    private void FxTrash_DragOver(object sender, DragEventArgs e)
    {
        if (_dragSlot is null)
        {
            return;
        }

        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Удалить";
        HideDropIndicator();
    }

    private void FxTrash_Drop(object sender, DragEventArgs e)
    {
        if (_dragSlot is not null)
        {
            EffectsChainViewModel.RemoveSlot(_dragSlot);
        }

        EndDrag();
        e.Handled = true;
    }

    private void ShowDropIndicator(int insertIndex)
    {
        HideDropIndicator();
        _activeDropLine = FindDropLineAt(insertIndex);

        if (_activeDropLine is null)
        {
            return;
        }

        _activeDropLine.Opacity = 1;
        _activeDropLine.Background = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = 0.35,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(450),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Storyboard.SetTarget(animation, _activeDropLine);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Begin();
        _activeDropLine.Tag = storyboard;
    }

    private Border? FindDropLineAt(int insertIndex)
    {
        if (insertIndex <= 0)
        {
            return FxDropIndicatorTop;
        }

        if (insertIndex >= EffectsChainViewModel.Chain.Count)
        {
            return FxDropIndicatorBottom;
        }

        var itemsPresenter = FindVisualChild<ItemsPresenter>(ChainItems);
        if (itemsPresenter is null)
        {
            return FxDropIndicatorBottom;
        }

        if (VisualTreeHelper.GetChildrenCount(itemsPresenter) == 0)
        {
            return FxDropIndicatorBottom;
        }

        var panel = VisualTreeHelper.GetChild(itemsPresenter, 0);
        if (insertIndex >= VisualTreeHelper.GetChildrenCount(panel))
        {
            return FxDropIndicatorBottom;
        }

        if (VisualTreeHelper.GetChild(panel, insertIndex) is FrameworkElement item)
        {
            return item.FindName("DropLine") as Border;
        }

        return FxDropIndicatorBottom;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; ++i)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void HideDropIndicator()
    {
        if (_activeDropLine?.Tag is Storyboard storyboard)
        {
            storyboard.Stop();
            _activeDropLine.Tag = null;
        }

        if (_activeDropLine is not null)
        {
            _activeDropLine.Opacity = 0;
        }

        FxDropIndicatorTop.Opacity = 0;
        FxDropIndicatorBottom.Opacity = 0;
        _activeDropLine = null;
    }

    private void EndDrag()
    {
        _dragSlot = null;
        FxTrashOverlay.Visibility = Visibility.Collapsed;
        HideDropIndicator();
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
