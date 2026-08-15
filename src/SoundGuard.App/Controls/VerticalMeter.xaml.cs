using System.Windows;
using System.Windows.Controls;

namespace SoundGuard.App.Controls;

/// <summary>
/// Reusable vertical bar meter with a bottom-up gradient fill. Displays <c>Minimum … Maximum</c>
/// on a linear scale; used for LUFS, dBFS and gain-reduction.
/// </summary>
public partial class VerticalMeter : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(VerticalMeter), new PropertyMetadata("", OnLabelChanged));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(VerticalMeter), new PropertyMetadata("", OnLabelChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(VerticalMeter), new PropertyMetadata(-60.0, OnScaleChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(VerticalMeter), new PropertyMetadata(0.0, OnScaleChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(VerticalMeter), new PropertyMetadata(double.NegativeInfinity, OnValueChanged));

    public VerticalMeter()
    {
        InitializeComponent();
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VerticalMeter)d).UpdateLabels();

    private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var meter = (VerticalMeter)d;
        meter.UpdateLabels();
        meter.UpdateBar();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VerticalMeter)d).UpdateBar();

    private void UpdateLabels()
    {
        TitleText.Text = Title;
        MinText.Text = $"{Minimum:0}";
        MaxText.Text = $"{Maximum:0}";
    }

    private void UpdateBar()
    {
        double v = Value;
        double fraction = 0.0;
        double range = Maximum - Minimum;

        if (!double.IsNaN(v) && !double.IsNegativeInfinity(v) && range > 0)
            fraction = Math.Clamp((v - Minimum) / range, 0.0, 1.0);

        BarScale.ScaleY = fraction;

        bool hasValue = !double.IsNaN(Value) && !double.IsNegativeInfinity(Value);
        ValueText.Text = hasValue ? $"{Value:0.0} {Unit}" : "---";
    }
}
