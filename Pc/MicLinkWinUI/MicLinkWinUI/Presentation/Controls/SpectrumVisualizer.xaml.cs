namespace MicLinkWinUI.Presentation.Controls;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

public sealed partial class SpectrumVisualizer : UserControl
{
    public static readonly DependencyProperty InputLevelProperty =
        DependencyProperty.Register(
            nameof(InputLevel),
            typeof(double),
            typeof(SpectrumVisualizer),
            new PropertyMetadata(0d, OnInputLevelChanged));

    public static readonly DependencyProperty IsVisualizationEnabledProperty =
        DependencyProperty.Register(
            nameof(IsVisualizationEnabled),
            typeof(bool),
            typeof(SpectrumVisualizer),
            new PropertyMetadata(true, OnVisualizationEnabledChanged));

    private const int BarCount = 24;
    private const double SilenceThreshold = 1.5;
    private const double MinVisibleHeight = 4d;

    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly float[] _targets = new float[BarCount];
    private readonly float[] _current = new float[BarCount];
    private readonly float[] _phase = new float[BarCount];
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();
    private double _lastInputLevel;

    public SpectrumVisualizer()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => LayoutBars();

        for (var i = 0; i < BarCount; ++i)
        {
            _phase[i] = (float)(_random.NextDouble() * Math.PI * 2);
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => AnimateFrame();
    }

    public double InputLevel
    {
        get => (double)GetValue(InputLevelProperty);
        set => SetValue(InputLevelProperty, value);
    }

    public bool IsVisualizationEnabled
    {
        get => (bool)GetValue(IsVisualizationEnabledProperty);
        set => SetValue(IsVisualizationEnabledProperty, value);
    }

    public event EventHandler? ToggleRequested;

    private static void OnInputLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumVisualizer visualizer)
        {
            visualizer._lastInputLevel = (double)e.NewValue;
            visualizer.UpdateTargets();
        }
    }

    private static void OnVisualizationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumVisualizer visualizer)
        {
            visualizer.DisabledOverlay.Visibility = visualizer.IsVisualizationEnabled
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureBars();
        LayoutBars();
        DisabledOverlay.Visibility = IsVisualizationEnabled ? Visibility.Collapsed : Visibility.Visible;
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureBars()
    {
        if (_bars[0] is not null)
        {
            return;
        }

        BarsCanvas.Children.Clear();
        for (var i = 0; i < BarCount; ++i)
        {
            var bar = new Rectangle
            {
                RadiusX = 3,
                RadiusY = 3,
                Fill = CreateBarBrush(i),
            };

            _bars[i] = bar;
            BarsCanvas.Children.Add(bar);
        }
    }

    private static Brush CreateBarBrush(int index)
    {
        var t = index / (double)(BarCount - 1);
        var r = (byte)(120 + t * 100);
        var g = (byte)(80 + (1 - Math.Abs(t - 0.5) * 2) * 120);
        var b = (byte)(200 - t * 120);
        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
    }

    private void LayoutBars()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || _bars[0] is null)
        {
            return;
        }

        var gap = 4d;
        var width = Math.Max(4d, (ActualWidth - gap * (BarCount - 1)) / BarCount);
        for (var i = 0; i < BarCount; ++i)
        {
            var bar = _bars[i];
            Canvas.SetLeft(bar, i * (width + gap));
            Canvas.SetTop(bar, ActualHeight);
            bar.Width = width;
            bar.Height = MinVisibleHeight;
        }
    }

    private void UpdateTargets()
    {
        if (_lastInputLevel < SilenceThreshold)
        {
            for (var i = 0; i < BarCount; ++i)
            {
                _targets[i] = 0f;
            }

            return;
        }

        var level = (float)Math.Clamp(Math.Pow(_lastInputLevel / 100d, 0.65) * 1.8, 0d, 1d);
        for (var i = 0; i < BarCount; ++i)
        {
            var band = 0.25f + 0.75f * (float)Math.Sin((i + 1) * 0.42 + _phase[i]);
            _targets[i] = Math.Clamp(level * band, 0f, 1f);
        }
    }

    private void AnimateFrame()
    {
        if (!IsVisualizationEnabled || ActualHeight <= 0 || _bars[0] is null)
        {
            return;
        }

        UpdateTargets();

        var smoothing = _lastInputLevel < SilenceThreshold ? 0.55f : 0.42f;
        for (var i = 0; i < BarCount; ++i)
        {
            _current[i] += (_targets[i] - _current[i]) * smoothing;
            var bar = _bars[i];
            var height = _current[i] <= 0.01f
                ? MinVisibleHeight
                : Math.Max(MinVisibleHeight, _current[i] * ActualHeight);
            bar.Height = height;
            Canvas.SetTop(bar, ActualHeight - height);
        }
    }
}
