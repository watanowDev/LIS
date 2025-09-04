using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WATA.LIS.Core.Behaviors
{
    // Attached behavior to auto-shrink font size so text fits within the element bounds
    public static class AutoFontSizeBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoFontSizeBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static readonly DependencyProperty MinFontSizeProperty = DependencyProperty.RegisterAttached(
            "MinFontSize",
            typeof(double),
            typeof(AutoFontSizeBehavior),
            new PropertyMetadata(8.0, OnLayoutAffectingPropertyChanged));

        public static void SetMinFontSize(DependencyObject element, double value) => element.SetValue(MinFontSizeProperty, value);
        public static double GetMinFontSize(DependencyObject element) => (double)element.GetValue(MinFontSizeProperty);

        public static readonly DependencyProperty MaxFontSizeProperty = DependencyProperty.RegisterAttached(
            "MaxFontSize",
            typeof(double),
            typeof(AutoFontSizeBehavior),
            new PropertyMetadata(double.NaN, OnLayoutAffectingPropertyChanged));

        public static void SetMaxFontSize(DependencyObject element, double value) => element.SetValue(MaxFontSizeProperty, value);
        public static double GetMaxFontSize(DependencyObject element) => (double)element.GetValue(MaxFontSizeProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                if ((bool)e.NewValue)
                {
                    fe.SizeChanged += OnLayoutChanged;
                    fe.LayoutUpdated += OnLayoutUpdated;

                    if (fe is TextBlock tb)
                    {
                        var dpd = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
                        dpd?.AddValueChanged(tb, OnContentChanged);
                    }
                    else if (fe is Label lb)
                    {
                        var dpd = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(Label));
                        dpd?.AddValueChanged(lb, OnContentChanged);
                    }
                }
                else
                {
                    fe.SizeChanged -= OnLayoutChanged;
                    fe.LayoutUpdated -= OnLayoutUpdated;

                    if (fe is TextBlock tb)
                    {
                        var dpd = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
                        dpd?.RemoveValueChanged(tb, OnContentChanged);
                    }
                    else if (fe is Label lb)
                    {
                        var dpd = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(Label));
                        dpd?.RemoveValueChanged(lb, OnContentChanged);
                    }
                }
            }
        }

        private static void OnLayoutAffectingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (GetIsEnabled(d) && d is FrameworkElement fe)
            {
                AdjustFontSize(fe);
            }
        }

        private static void OnLayoutChanged(object? sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement fe && GetIsEnabled(fe))
            {
                AdjustFontSize(fe);
            }
        }

        private static void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement fe && GetIsEnabled(fe))
            {
                AdjustFontSize(fe);
            }
        }

        private static void OnContentChanged(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement fe && GetIsEnabled(fe))
            {
                AdjustFontSize(fe);
            }
        }

        private static void AdjustFontSize(FrameworkElement fe)
        {
            if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0)
                return;

            string? text = fe switch
            {
                TextBlock tb => tb.Text,
                Label lb => lb.Content?.ToString(),
                _ => null
            };
            if (string.IsNullOrEmpty(text)) return;

            double availableWidth = fe.ActualWidth;
            double availableHeight = fe.ActualHeight;
            if (fe is Control ctl)
            {
                var p = ctl.Padding;
                availableWidth = Math.Max(0, availableWidth - p.Left - p.Right);
                availableHeight = Math.Max(0, availableHeight - p.Top - p.Bottom);
            }
            if (availableWidth <= 0 || availableHeight <= 0) return;

            var typeface = CreateTypeface(fe);
            double startFont = (fe as Control)?.FontSize > 0 ? (fe as Control)!.FontSize : (fe as TextBlock)?.FontSize ?? 12.0;
            double min = GetMinFontSize(fe);
            double max = GetMaxFontSize(fe);
            if (double.IsNaN(max) || max <= 0) max = startFont; // default shrink-only
            if (min <= 0 || min > max) min = Math.Min(8.0, max);

            double dpi = VisualTreeHelper.GetDpi(fe).PixelsPerDip;
            double lo = min, hi = max, best = min;
            for (int i = 0; i < 16; i++)
            {
                double mid = (lo + hi) / 2.0;
                if (Fits(text!, typeface, mid, dpi, availableWidth, availableHeight))
                {
                    best = mid;
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
                if (Math.Abs(hi - lo) < 0.1) break;
            }

            if (fe is Control c)
                c.FontSize = Math.Round(best, 1);
            else if (fe is TextBlock t)
                t.FontSize = Math.Round(best, 1);
        }

        private static bool Fits(string text, Typeface typeface, double fontSize, double dpi, double maxWidth, double maxHeight)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Transparent,
                dpi);

            return formatted.Width <= maxWidth && formatted.Height <= maxHeight;
        }

        private static Typeface CreateTypeface(FrameworkElement fe)
        {
            var fontFamily = (fe as Control)?.FontFamily ?? (fe as TextBlock)?.FontFamily ?? new FontFamily("Segoe UI");
            var weight = (fe as Control)?.FontWeight ?? (fe as TextBlock)?.FontWeight ?? FontWeights.Normal;
            var style = (fe as Control)?.FontStyle ?? (fe as TextBlock)?.FontStyle ?? FontStyles.Normal;
            var stretch = (fe as Control)?.FontStretch ?? (fe as TextBlock)?.FontStretch ?? FontStretches.Normal;
            return new Typeface(fontFamily, style, weight, stretch);
        }
    }
}
