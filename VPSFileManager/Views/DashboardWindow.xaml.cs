using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using VPSFileManager.ViewModels;

namespace VPSFileManager.Views
{
    public partial class DashboardWindow : Wpf.Ui.Controls.FluentWindow
    {
        private DashboardViewModel? _viewModel;

        public DashboardWindow()
        {
            InitializeComponent();
        }

        public async void StartMonitoring(DashboardViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            try
            {
                await viewModel.StartMonitoringAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard monitoring stopped: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.StopMonitoring();
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Returns true if value >= threshold parameter (for color-coding).
    /// Usage: Converter={x:Static local:ThresholdConverter.Instance}, ConverterParameter=70
    /// </summary>
    public class ThresholdConverter : IValueConverter
    {
        public static readonly ThresholdConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dVal && parameter is string sParam &&
                double.TryParse(sParam, NumberStyles.Any, CultureInfo.InvariantCulture, out double threshold))
            {
                return dVal >= threshold;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a percentage (0-100) to StrokeDashArray for donut chart.
    /// For Ellipse Width=90, Height=90, StrokeThickness=10:
    /// - Path radius (center of stroke) = 45
    /// - Circumference in pixels = 2 * PI * 45 ≈ 282.74
    /// - StrokeDashArray uses units of StrokeThickness, so: 282.74 / 10 ≈ 28.274
    /// </summary>
    public class DonutStrokeConverter : IValueConverter
    {
        public static readonly DonutStrokeConverter Instance = new();

        // Circumference in StrokeThickness units (282.74 / 10)
        private const double CircumferenceUnits = 28.274;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;
            if (value is double d) percent = d;
            else if (value is int i) percent = i;

            percent = Math.Max(0, Math.Min(100, percent));
            var filled = CircumferenceUnits * (percent / 100.0);
            var gap = CircumferenceUnits - filled + 0.01; // small epsilon to ensure full coverage

            // Return as DoubleCollection for StrokeDashArray
            return new System.Windows.Media.DoubleCollection { filled, gap };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
