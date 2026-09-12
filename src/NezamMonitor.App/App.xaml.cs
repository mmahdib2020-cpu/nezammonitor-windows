using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using NezamMonitor.App.Services;

namespace NezamMonitor.App;
public partial class App : Application
{
    private static NavigationService? _nav;
    public static NavigationService Navigation => _nav ?? throw new InvalidOperationException("Navigation not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _nav = new NavigationService();

        // Create MainWindow FIRST - it owns the application lifecycle
        var mainWindow = new MainWindow();
        Application.Current.MainWindow = mainWindow;

        // Create welcome overlay ON TOP of MainWindow
        var welcome = new Window
        {
            Title = "خوش‌آمدگویی",
            Width = 600,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White,
            AllowsTransparency = true,
            Opacity = 0.95,
            Topmost = true,
            ShowInTaskbar = false,
            Owner = mainWindow
        };

        var border = new System.Windows.Controls.Border
        {
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4F72")),
            BorderThickness = new Thickness(2),
            Background = Brushes.White
        };

        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });

        var header = new System.Windows.Controls.Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4F72")),
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(20, 15, 20, 15)
        };
        header.Child = new System.Windows.Controls.TextBlock
        {
            Text = "🔄 نظارت مهندسی",
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var message = new System.Windows.Controls.TextBlock
        {
            Text = "این نرم‌افزار توسط مهندس برخورداری به‌صورت اختصاصی برای مهندس مجتبی پورمروتی شریف‌آبادی تهیه و توسعه یافته است.\n\nهرگونه استفاده توسط سایر اشخاص، بدون کسب اجازه، قانوناً غیرمجاز و شرعاً حرام می‌باشد.",
            FontFamily = new FontFamily("B Nazanin"),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(30, 20, 30, 20),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50"))
        };

        var closeBtn = new System.Windows.Controls.Button
        {
            Content = "بستن",
            Width = 120,
            Height = 40,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 20),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Close button only closes the welcome overlay
        closeBtn.Click += (s, args) => { welcome.Close(); };

        grid.Children.Add(header);
        System.Windows.Controls.Grid.SetRow(header, 0);
        grid.Children.Add(message);
        System.Windows.Controls.Grid.SetRow(message, 1);
        grid.Children.Add(closeBtn);
        System.Windows.Controls.Grid.SetRow(closeBtn, 2);

        border.Child = grid;
        welcome.Content = border;

        // 3-second timer to auto-dismiss welcome
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        bool dismissed = false;
        timer.Tick += (s, args) =>
        {
            timer.Stop();
            if (!dismissed)
            {
                dismissed = true;
                // Fade out
                var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                int step = 0;
                fadeTimer.Tick += (s2, a2) =>
                {
                    step++;
                    welcome.Opacity = Math.Max(0, 1.0 - (step / 10.0));
                    if (welcome.Opacity <= 0)
                    {
                        fadeTimer.Stop();
                        welcome.Close();
                    }
                };
                fadeTimer.Start();
            }
        };

        // Override closing to prevent app shutdown
        welcome.Closing += (s, args) =>
        {
            dismissed = true;
            timer.Stop();
            // Don't cancel - let it close, but don't shut down app
        };

        // Show MainWindow first, then overlay
        mainWindow.Show();
        welcome.Show();
        timer.Start();
    }
}
