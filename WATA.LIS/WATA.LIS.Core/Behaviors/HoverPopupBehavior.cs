using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace WATA.LIS.Core.Behaviors
{
    // Keeps a Popup open when mouse is over the anchor or the popup itself, with small delays to avoid flicker
    public static class HoverPopupBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(HoverPopupBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static readonly DependencyProperty AnchorElementProperty = DependencyProperty.RegisterAttached(
            "AnchorElement", typeof(FrameworkElement), typeof(HoverPopupBehavior), new PropertyMetadata(null));

        public static void SetAnchorElement(DependencyObject element, FrameworkElement value) => element.SetValue(AnchorElementProperty, value);
        public static FrameworkElement GetAnchorElement(DependencyObject element) => (FrameworkElement)element.GetValue(AnchorElementProperty);

        public static readonly DependencyProperty OpenDelayProperty = DependencyProperty.RegisterAttached(
            "OpenDelay", typeof(int), typeof(HoverPopupBehavior), new PropertyMetadata(80));

        public static void SetOpenDelay(DependencyObject element, int value) => element.SetValue(OpenDelayProperty, value);
        public static int GetOpenDelay(DependencyObject element) => (int)element.GetValue(OpenDelayProperty);

        public static readonly DependencyProperty CloseDelayProperty = DependencyProperty.RegisterAttached(
            "CloseDelay", typeof(int), typeof(HoverPopupBehavior), new PropertyMetadata(150));

        public static void SetCloseDelay(DependencyObject element, int value) => element.SetValue(CloseDelayProperty, value);
        public static int GetCloseDelay(DependencyObject element) => (int)element.GetValue(CloseDelayProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Popup popup)
                return;

            bool enabled = (bool)e.NewValue;
            if (enabled)
            {
                EnsureHandlers(popup);
            }
            else
            {
                RemoveHandlers(popup);
            }
        }

        private class State
        {
            public FrameworkElement Anchor;
            public Popup Popup;
            public DispatcherTimer OpenTimer;
            public DispatcherTimer CloseTimer;
        }

        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "_HoverState", typeof(State), typeof(HoverPopupBehavior), new PropertyMetadata(null));

        private static void EnsureHandlers(Popup popup)
        {
            var anchor = GetAnchorElement(popup);
            if (anchor == null)
            {
                // wait until loaded to resolve anchor again
                popup.Loaded += Popup_Loaded;
                return;
            }

            popup.StaysOpen = true; // we'll manage closing

            var state = new State
            {
                Anchor = anchor,
                Popup = popup,
                OpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GetOpenDelay(popup)) },
                CloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GetCloseDelay(popup)) }
            };
            state.OpenTimer.Tick += (s, e) =>
            {
                state.OpenTimer.Stop();
                popup.IsOpen = true;
            };
            state.CloseTimer.Tick += (s, e) =>
            {
                state.CloseTimer.Stop();
                if (!IsMouseOverAny(state))
                    popup.IsOpen = false;
            };

            anchor.MouseEnter += (s, e) => StartOpen(state);
            anchor.MouseLeave += (s, e) => StartClose(state);
            popup.MouseEnter += (s, e) => StartOpen(state);
            popup.MouseLeave += (s, e) => StartClose(state);

            popup.SetValue(StateProperty, state);
        }

        private static void Popup_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Popup popup)
            {
                popup.Loaded -= Popup_Loaded;
                EnsureHandlers(popup);
            }
        }

        private static void RemoveHandlers(Popup popup)
        {
            if (popup.GetValue(StateProperty) is State state)
            {
                state.OpenTimer?.Stop();
                state.CloseTimer?.Stop();
                popup.ClearValue(StateProperty);
            }
        }

        private static void StartOpen(State state)
        {
            state.CloseTimer?.Stop();
            if (state.Popup.IsOpen)
                return;
            state.OpenTimer?.Stop();
            state.OpenTimer?.Start();
        }

        private static void StartClose(State state)
        {
            state.OpenTimer?.Stop();
            state.CloseTimer?.Stop();
            state.CloseTimer?.Start();
        }

        private static bool IsMouseOverAny(State state)
        {
            // Popup.IsMouseOver works even though Popup is rendered in its own window
            return (state.Anchor?.IsMouseOver ?? false) || (state.Popup?.IsMouseOver ?? false);
        }
    }
}
