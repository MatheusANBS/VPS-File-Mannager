using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VPSFileManager.Controls
{
    /// <summary>
    /// Lightweight sparkline chart for CPU/Memory history.
    /// Draws a filled area chart from a collection of 0-100 values.
    /// </summary>
    public class SparklineControl : Control
    {
        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(nameof(Values), typeof(ObservableCollection<double>), typeof(SparklineControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

        public static readonly DependencyProperty StrokeColorProperty =
            DependencyProperty.Register(nameof(StrokeColor), typeof(Color), typeof(SparklineControl),
                new FrameworkPropertyMetadata(Colors.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillColorProperty =
            DependencyProperty.Register(nameof(FillColor), typeof(Color), typeof(SparklineControl),
                new FrameworkPropertyMetadata(Color.FromArgb(40, 30, 144, 255), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridLineColorProperty =
            DependencyProperty.Register(nameof(GridLineColor), typeof(Color), typeof(SparklineControl),
                new FrameworkPropertyMetadata(Color.FromArgb(30, 255, 255, 255), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(SparklineControl),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public ObservableCollection<double> Values
        {
            get => (ObservableCollection<double>)GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public Color StrokeColor
        {
            get => (Color)GetValue(StrokeColorProperty);
            set => SetValue(StrokeColorProperty, value);
        }

        public Color FillColor
        {
            get => (Color)GetValue(FillColorProperty);
            set => SetValue(FillColorProperty, value);
        }

        public Color GridLineColor
        {
            get => (Color)GetValue(GridLineColorProperty);
            set => SetValue(GridLineColorProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (SparklineControl)d;
            if (e.OldValue is ObservableCollection<double> oldColl)
                oldColl.CollectionChanged -= ctrl.OnCollectionChanged;
            if (e.NewValue is ObservableCollection<double> newColl)
                newColl.CollectionChanged += ctrl.OnCollectionChanged;
            ctrl.InvalidateVisual();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Background
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(255, 15, 15, 15)), null, new Rect(0, 0, w, h));

            // Grid lines at 25%, 50%, 75%
            var gridPen = new Pen(new SolidColorBrush(GridLineColor), 1);
            gridPen.Freeze();
            for (int i = 1; i <= 3; i++)
            {
                var y = h - (h * (i * 25.0 / MaxValue));
                dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }

            // Y-axis labels
            var labelBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            labelBrush.Freeze();
            for (int i = 0; i <= 4; i++)
            {
                var val = (int)(i * 25);
                var y = h - (h * (val / MaxValue));
                var text = new FormattedText(
                    val.ToString(),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    9,
                    labelBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(text, new Point(2, y - text.Height - 1));
            }

            var values = Values;
            if (values == null || values.Count < 2) return;

            var count = values.Count;
            var stepX = w / Math.Max(count - 1, 1);

            // Build points
            var points = new Point[count];
            for (int i = 0; i < count; i++)
            {
                var val = Math.Min(Math.Max(values[i], 0), MaxValue);
                var x = i * stepX;
                var y = h - (h * (val / MaxValue));
                points[i] = new Point(x, y);
            }

            // Fill area
            var fillGeo = new StreamGeometry();
            using (var ctx = fillGeo.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, h), true, true);
                for (int i = 0; i < count; i++)
                    ctx.LineTo(points[i], false, false);
                ctx.LineTo(new Point(points[count - 1].X, h), false, false);
            }
            fillGeo.Freeze();

            var fillBrush = new LinearGradientBrush(
                FillColor,
                Color.FromArgb(5, FillColor.R, FillColor.G, FillColor.B),
                new Point(0, 0), new Point(0, 1));
            fillBrush.Freeze();
            dc.DrawGeometry(fillBrush, null, fillGeo);

            // Stroke line
            var lineGeo = new StreamGeometry();
            using (var ctx = lineGeo.Open())
            {
                ctx.BeginFigure(points[0], false, false);
                for (int i = 1; i < count; i++)
                    ctx.LineTo(points[i], true, false);
            }
            lineGeo.Freeze();

            var strokeBrush = new SolidColorBrush(StrokeColor);
            strokeBrush.Freeze();
            var linePen = new Pen(strokeBrush, 1.5);
            linePen.Freeze();
            dc.DrawGeometry(null, linePen, lineGeo);

            // Current value dot
            var lastPt = points[count - 1];
            dc.DrawEllipse(strokeBrush, null, lastPt, 3, 3);
        }
    }
}
