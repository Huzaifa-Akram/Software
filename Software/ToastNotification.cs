using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Software
{
    public class ToastNotification
    {
        private readonly Border _notificationPanel;
        private readonly DispatcherTimer _timer;
        private readonly Window _parentWindow;

        public enum NotificationType
        {
            Success,
            Error,
            Warning,
            Information
        }

        public ToastNotification(Window parentWindow)
        {
            _parentWindow = parentWindow;

            // Create notification panel
            _notificationPanel = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(16),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Opacity = 0,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 2,
                    Opacity = 0.6,
                    BlurRadius = 10
                },
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                }
            };

            // Add notification panel to parent window
            if (_parentWindow is Window window)
            {
                Grid parentGrid = new Grid();

                if (window.Content is UIElement existingContent)
                {
                    window.Content = null;
                    parentGrid.Children.Add(existingContent);
                }

                parentGrid.Children.Add(_notificationPanel);
                window.Content = parentGrid;

                Panel.SetZIndex(_notificationPanel, 9999); // Ensure it's on top
            }

            // Timer for auto-close
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3) // Default duration
            };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                HideNotification();
            };
        }

        public void Show(string message, NotificationType type = NotificationType.Success, int durationSeconds = 3)
        {
            // Update timer duration
            _timer.Interval = TimeSpan.FromSeconds(durationSeconds);

            // Get icon and color based on notification type
            var iconContent = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };

            SolidColorBrush accentColor;

            switch (type)
            {
                case NotificationType.Success:
                    iconContent.Text = "✓"; // Checkmark
                    accentColor = new SolidColorBrush(Colors.LimeGreen);
                    break;
                case NotificationType.Error:
                    iconContent.Text = "❌"; // X mark
                    accentColor = new SolidColorBrush(Colors.Red);
                    break;
                case NotificationType.Warning:
                    iconContent.Text = "⚠"; // Warning
                    accentColor = new SolidColorBrush(Colors.Orange);
                    break;
                case NotificationType.Information:
                default:
                    iconContent.Text = "ℹ"; // Info
                    accentColor = new SolidColorBrush(Colors.DeepSkyBlue);
                    break;
            }

            // Create content
            var stackPanel = (StackPanel)_notificationPanel.Child;
            stackPanel.Children.Clear();

            // Icon
            Border iconBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = accentColor,
                Child = iconContent
            };

            // Align icon content in center
            iconContent.HorizontalAlignment = HorizontalAlignment.Center;

            stackPanel.Children.Add(iconBorder);

            // Message
            TextBlock messageText = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 250,
                Margin = new Thickness(12, 0, 0, 0) // Left margin of 12 pixels
            };
            stackPanel.Children.Add(messageText);

            // Show notification
            ShowNotification();
        }

        private void ShowNotification()
        {
            // Start fade-in animation
            DoubleAnimation fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            // Start slide-in animation
            ThicknessAnimation slideIn = new ThicknessAnimation
            {
                From = new Thickness(16, 16, -250, 16), // Start from right (outside view)
                To = new Thickness(16),
                Duration = TimeSpan.FromMilliseconds(300)
            };

            _notificationPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            _notificationPanel.BeginAnimation(Border.MarginProperty, slideIn);

            // Start timer for auto-close
            _timer.Start();
        }

        private void HideNotification()
        {
            // Start fade-out animation
            DoubleAnimation fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            // Start slide-out animation
            ThicknessAnimation slideOut = new ThicknessAnimation
            {
                From = new Thickness(16),
                To = new Thickness(16, 16, -250, 16), // Move right (outside view)
                Duration = TimeSpan.FromMilliseconds(300)
            };

            _notificationPanel.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            _notificationPanel.BeginAnimation(Border.MarginProperty, slideOut);
        }
    }
}
