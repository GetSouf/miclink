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
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

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
    private ListViewItem? _dragOverItem;
    private bool _isFxDragging;
    private bool _deleteOverlayHover;
    private readonly Random _noiseRandom = new();

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

        ChainList.ContainerContentChanging += ChainList_ContainerContentChanging;
    }

    private void ChainList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem item)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            DetachChainItemHandlers(item);
            return;
        }

        args.RegisterUpdateCallback((_, changingArgs) =>
        {
            if (changingArgs.ItemContainer is ListViewItem listItem)
            {
                DetachChainItemHandlers(listItem);
                AttachChainItemHandlers(listItem);
            }
        });
    }

    private void AttachChainItemHandlers(ListViewItem item)
    {
        item.DragStarting += ChainItem_DragStarting;
        item.DragOver += ChainItem_DragOver;
        item.DragLeave += ChainItem_DragLeave;
        item.Drop += ChainItem_Drop;
    }

    private void DetachChainItemHandlers(ListViewItem item)
    {
        item.DragStarting -= ChainItem_DragStarting;
        item.DragOver -= ChainItem_DragOver;
        item.DragLeave -= ChainItem_DragLeave;
        item.Drop -= ChainItem_Drop;
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

    private void ChainItem_DragStarting(object sender, DragStartingEventArgs e)
    {
        if (sender is not ListViewItem { Content: EffectSlotViewModel slot })
        {
            return;
        }

        _dragSlot = slot;
        _isFxDragging = true;
        e.Data.SetText(slot.SlotId);
        e.DragUI.SetContentFromDataPackage();
        ShowParamsDragOverlay();
    }

    private void ChainItem_DragOver(object sender, DragEventArgs e)
    {
        if (!_isFxDragging || _dragSlot is null)
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.Caption = "Переместить";
        e.Handled = true;

        if (sender is ListViewItem item &&
            item.Content is EffectSlotViewModel target &&
            !ReferenceEquals(_dragSlot, target))
        {
            SetDragOverItem(item);
        }
    }

    private void ChainItem_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListViewItem item && ReferenceEquals(_dragOverItem, item))
        {
            ClearDragOverItem();
        }
    }

    private void ChainItem_Drop(object sender, DragEventArgs e)
    {
        if (!_isFxDragging || _dragSlot is null)
        {
            EndFxDrag();
            return;
        }

        if (sender is ListViewItem { Content: EffectSlotViewModel target } &&
            !ReferenceEquals(_dragSlot, target))
        {
            var insertIndex = EffectsChainViewModel.Chain.IndexOf(target);
            if (e.GetPosition((UIElement)sender).Y > ((FrameworkElement)sender).ActualHeight / 2)
            {
                insertIndex++;
            }

            EffectsChainViewModel.MoveSlot(_dragSlot, insertIndex);
        }

        e.Handled = true;
        EndFxDrag();
    }

    private void ChainList_DragOver(object sender, DragEventArgs e)
    {
        if (!_isFxDragging || _dragSlot is null)
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        e.Handled = true;
    }

    private void ChainList_Drop(object sender, DragEventArgs e)
    {
        if (!_isFxDragging || _dragSlot is null)
        {
            EndFxDrag();
            return;
        }

        EffectsChainViewModel.MoveSlot(_dragSlot, EffectsChainViewModel.Chain.Count);
        e.Handled = true;
        EndFxDrag();
    }

    private void FxParamsDragOverlay_DragOver(object sender, DragEventArgs e)
    {
        if (!_isFxDragging || _dragSlot is null)
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.Caption = "Удалить";
        SetDeleteOverlayHover(true);
        e.Handled = true;
    }

    private void FxParamsDragOverlay_DragLeave(object sender, DragEventArgs e)
    {
        SetDeleteOverlayHover(false);
    }

    private void FxParamsDragOverlay_Drop(object sender, DragEventArgs e)
    {
        if (_dragSlot is not null)
        {
            EffectsChainViewModel.RemoveSlot(_dragSlot);
        }

        e.Handled = true;
        EndFxDrag();
    }

    private void ShowParamsDragOverlay()
    {
        PopulateDragNoise();
        FxParamsDragOverlay.Visibility = Visibility.Visible;
        FxParamsDragOverlay.Opacity = 0;
        SetDeleteOverlayHover(false);

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fadeIn, FxParamsDragOverlay);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeIn);
        storyboard.Begin();
    }

    private void HideParamsDragOverlay()
    {
        FxParamsDragOverlay.Visibility = Visibility.Collapsed;
        FxParamsDragOverlay.Opacity = 1;
        FxDragNoiseCanvas.Children.Clear();
        SetDeleteOverlayHover(false);
    }

    private void PopulateDragNoise()
    {
        FxDragNoiseCanvas.Children.Clear();
        var width = FxParamsCard.ActualWidth > 0 ? FxParamsCard.ActualWidth : 280;
        var height = FxParamsCard.ActualHeight > 0 ? FxParamsCard.ActualHeight : 420;

        for (var i = 0; i < 420; ++i)
        {
            var dot = new Rectangle
            {
                Width = 1 + _noiseRandom.Next(2),
                Height = 1 + _noiseRandom.Next(2),
                Fill = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        (byte)_noiseRandom.Next(18, 75),
                        (byte)_noiseRandom.Next(180, 255),
                        (byte)_noiseRandom.Next(40, 100),
                        (byte)_noiseRandom.Next(40, 90))),
            };

            Canvas.SetLeft(dot, _noiseRandom.NextDouble() * width);
            Canvas.SetTop(dot, _noiseRandom.NextDouble() * height);
            FxDragNoiseCanvas.Children.Add(dot);
        }
    }

    private void SetDeleteOverlayHover(bool hover)
    {
        if (_deleteOverlayHover == hover)
        {
            return;
        }

        _deleteOverlayHover = hover;
        FxParamsDragOverlay.Background = new SolidColorBrush(
            hover
                ? Windows.UI.Color.FromArgb(235, 48, 12, 14)
                : Windows.UI.Color.FromArgb(217, 18, 18, 24));
        FxParamsDragOverlay.BorderBrush = new SolidColorBrush(
            hover
                ? Windows.UI.Color.FromArgb(255, 231, 76, 60)
                : Windows.UI.Color.FromArgb(102, 231, 76, 60));
        FxDeleteIcon.Foreground = new SolidColorBrush(
            hover
                ? Windows.UI.Color.FromArgb(255, 255, 120, 100)
                : Windows.UI.Color.FromArgb(255, 231, 76, 60));
        FxDeleteHint.Text = hover ? "Отпустите — удалить" : "Перетащите сюда для удаления";
    }

    private void SetDragOverItem(ListViewItem item)
    {
        if (ReferenceEquals(_dragOverItem, item))
        {
            return;
        }

        ClearDragOverItem();
        _dragOverItem = item;
        item.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 231, 76, 60));
    }

    private void ClearDragOverItem()
    {
        if (_dragOverItem is null)
        {
            return;
        }

        _dragOverItem.Background = null;
        _dragOverItem = null;
    }

    private void EndFxDrag()
    {
        if (!_isFxDragging)
        {
            return;
        }

        _isFxDragging = false;
        _dragSlot = null;
        ClearDragOverItem();
        HideParamsDragOverlay();
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
