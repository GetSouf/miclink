using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Core.DependencyInjection;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Infrastructure.Theming;
using MicLinkWinUI.Presentation.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using System.Runtime.InteropServices;
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
    private bool _isFxDragging;
    private bool _deleteOverlayHover;
    private bool _overlayHideRunning;
    private ListViewItem? _draggedContainer;
    private double _draggedPlaceholderHeight;
    private readonly Random _noiseRandom = new();

    private const int ReorderAnimMs = 120;
    private const int OverlayFadeInMs = 70;
    private const int OverlayFadeOutMs = 180;
    private const int ParamsRestoreMs = 200;
    private const int ChainDragStartMs = 80;
    private const int ChainRestoreMs = 140;
    private const float ChainDragScale = 0.96f;
    private const float DraggedPlaceholderOpacity = 0.38f;

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
        if (args.Items.FirstOrDefault() is not EffectSlotViewModel slot)
        {
            return;
        }

        _dragSlot = slot;
        _isFxDragging = true;
        _deleteOverlayHover = false;
        ShowDeleteOverlay();
        BeginChainDragVisuals(slot);
    }

    private void ChainList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
        {
            if (args.ItemContainer is ListViewItem recycled)
            {
                recycled.DragStarting -= ChainItem_DragStarting;
            }

            return;
        }

        args.RegisterUpdateCallback((_, changingArgs) =>
        {
            if (changingArgs.ItemContainer is not ListViewItem item)
            {
                return;
            }

            item.DragStarting -= ChainItem_DragStarting;
            item.DragStarting += ChainItem_DragStarting;
            ApplyFastReorderAnimation(item);
        });
    }

    private void ChainItem_DragStarting(object sender, DragStartingEventArgs args)
    {
        args.AllowedOperations = DataPackageOperation.Move;
    }

    private static void ApplyFastReorderAnimation(ListViewItem item)
    {
        var visual = ElementCompositionPreview.GetElementVisual(item);
        var compositor = visual.Compositor;
        UpdateVisualCenter(item, visual);

        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Target = "Offset";
        offsetAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(ReorderAnimMs);

        var collection = compositor.CreateImplicitAnimationCollection();
        collection["Offset"] = offsetAnimation;
        visual.ImplicitAnimations = collection;
    }

    private void BeginChainDragVisuals(EffectSlotViewModel slot)
    {
        ForEachChainContainer((item, _) =>
        {
            AnimateItemScale(item, ChainDragScale, ChainDragStartMs);
        });

        var queue = DispatcherQueue.GetForCurrentThread();
        queue.TryEnqueue(() =>
        {
            if (!_isFxDragging || _dragSlot != slot)
            {
                return;
            }

            if (ChainList.ContainerFromItem(slot) is not ListViewItem container)
            {
                return;
            }

            _draggedContainer = container;
            _draggedPlaceholderHeight = container.ActualHeight > 0 ? container.ActualHeight : 56;
            ApplyDraggedPlaceholder(container);

            queue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (_draggedContainer == container && _isFxDragging)
                {
                    ApplyDraggedPlaceholder(container);
                }
            });
        });
    }

    private void ApplyDraggedPlaceholder(ListViewItem container)
    {
        container.MinHeight = _draggedPlaceholderHeight > 0 ? _draggedPlaceholderHeight : 56;
        AnimateItemScale(container, ChainDragScale, ChainDragStartMs);
        AnimateItemOpacity(container, DraggedPlaceholderOpacity, ChainDragStartMs);
    }

    private void RestoreChainItemVisuals()
    {
        ForEachChainContainer((item, _) =>
        {
            item.MinHeight = 0;
            AnimateItemScale(item, 1f, ChainRestoreMs);
            AnimateItemOpacity(item, 1.0, ChainRestoreMs);
        });

        if (_draggedContainer is { } dragged)
        {
            dragged.MinHeight = 0;
            AnimateItemScale(dragged, 1f, ChainRestoreMs);
            AnimateItemOpacity(dragged, 1.0, ChainRestoreMs);
        }

        _draggedContainer = null;
        _draggedPlaceholderHeight = 0;
    }

    private void ForEachChainContainer(Action<ListViewItem, EffectSlotViewModel> action)
    {
        foreach (var slot in EffectsChainViewModel.Chain)
        {
            if (ChainList.ContainerFromItem(slot) is ListViewItem item)
            {
                action(item, slot);
            }
        }
    }

    private static void UpdateVisualCenter(ListViewItem item, Microsoft.UI.Composition.Visual visual)
    {
        var width = (float)Math.Max(item.ActualWidth, 1);
        var height = (float)Math.Max(item.ActualHeight, 1);
        visual.CenterPoint = new System.Numerics.Vector3(width * 0.5f, height * 0.5f, 0);
    }

    private static void AnimateItemScale(ListViewItem item, float toScale, int durationMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(item);
        UpdateVisualCenter(item, visual);
        var compositor = visual.Compositor;

        var animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Target = "Scale";
        animation.InsertKeyFrame(0, visual.Scale);
        animation.InsertKeyFrame(1, new System.Numerics.Vector3(toScale, toScale, 1));
        animation.Duration = TimeSpan.FromMilliseconds(durationMs);
        visual.StartAnimation("Scale", animation);
    }

    private static void AnimateItemOpacity(ListViewItem item, double toOpacity, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, item);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void ChainList_DragItemsCompleted(object sender, DragItemsCompletedEventArgs args)
    {
        if (_dragSlot is null)
        {
            EndFxDrag();
            return;
        }

        if (_deleteOverlayHover || IsPointerOverDeleteOverlay())
        {
            EffectsChainViewModel.RemoveSlot(_dragSlot);
        }
        else if (EffectsChainViewModel.Chain.Contains(_dragSlot))
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            queue.TryEnqueue(DispatcherQueuePriority.Low, EffectsChainViewModel.OnChainReordered);
        }

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

    private void ShowDeleteOverlay()
    {
        PopulateDragNoise();
        FxParamsDragOverlay.Visibility = Visibility.Visible;
        FxParamsDragOverlay.Opacity = 0;
        SetDeleteOverlayHover(false);

        AnimateDouble(FxParamsDragOverlay, 0, 1, OverlayFadeInMs, EasingMode.EaseOut);
        AnimateDouble(FxParamsToolbar, FxParamsToolbar.Opacity, 0.35, OverlayFadeInMs, EasingMode.EaseOut);
        AnimateDouble(FxParamsContent, FxParamsContent.Opacity, 0.35, OverlayFadeInMs, EasingMode.EaseOut);
    }

    private void HideDeleteOverlay()
    {
        if (_overlayHideRunning || FxParamsDragOverlay.Visibility != Visibility.Visible)
        {
            ResetDeleteOverlayVisuals();
            return;
        }

        _overlayHideRunning = true;
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var storyboard = new Storyboard();
        storyboard.Completed += (_, _) =>
        {
            ResetDeleteOverlayVisuals();
            _overlayHideRunning = false;
        };

        AddFade(storyboard, FxParamsDragOverlay, FxParamsDragOverlay.Opacity, 0, OverlayFadeOutMs, easing);
        AddFade(storyboard, FxParamsToolbar, FxParamsToolbar.Opacity, 1, ParamsRestoreMs, easing);
        AddFade(storyboard, FxParamsContent, FxParamsContent.Opacity, 1, ParamsRestoreMs, easing);
        storyboard.Begin();
    }

    private void ResetDeleteOverlayVisuals()
    {
        FxParamsDragOverlay.Visibility = Visibility.Collapsed;
        FxParamsDragOverlay.Opacity = 1;
        FxParamsToolbar.Opacity = 1;
        FxParamsContent.Opacity = 1;
        FxDragNoiseCanvas.Children.Clear();
        SetDeleteOverlayHover(false);
    }

    private static void AnimateDouble(
        UIElement target,
        double from,
        double to,
        int durationMs,
        EasingMode easingMode)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = easingMode },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static void AddFade(
        Storyboard storyboard,
        UIElement target,
        double from,
        double to,
        int durationMs,
        EasingFunctionBase easing)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = easing,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
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

    private bool IsPointerOverDeleteOverlay()
    {
        if (FxParamsDragOverlay.Visibility != Visibility.Visible)
        {
            return false;
        }

        if (_deleteOverlayHover)
        {
            return true;
        }

        return IsPointInElement(FxParamsDragOverlay, GetCursorPoint());
    }

    private Point GetCursorPoint()
    {
        if (!GetCursorPos(out var cursor))
        {
            return new Point(double.MinValue, double.MinValue);
        }

        return new Point(cursor.X, cursor.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    private static bool IsPointInElement(FrameworkElement element, Point point)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return false;
        }

        var transform = element.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return bounds.Contains(point);
    }

    private void EndFxDrag()
    {
        if (!_isFxDragging)
        {
            return;
        }

        _isFxDragging = false;
        RestoreChainItemVisuals();
        _dragSlot = null;
        HideDeleteOverlay();
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
