using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class UpdateView : UserControl
{
    private readonly UpdateViewModel _vm;
    private readonly DispatcherTimer _uiTimer;

    public UpdateView()
    {
        InitializeComponent();
        _vm = new UpdateViewModel(DatabaseService.Instance);
        DataContext = _vm;

        // Timer updates button states every 500ms
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (s, e) => UpdateButtonStates();

        // Start/stop timer based on visibility to prevent leaks & crashes
        Loaded += (_, _) =>
        {
            _uiTimer.Start();
            UpdateButtonStates();
        };
        Unloaded += (_, _) =>
        {
            _uiTimer.Stop();
        };
    }

    private void UpdateButtonStates()
    {
        try
        {
            var ec = ExtractionController.Instance;
            if (ec == null) return;

            // Start: enabled only when NOT busy
            BtnStart.IsEnabled = !ec.IsBusy;

            // Stop: enabled only when busy
            BtnStop.IsEnabled = ec.IsBusy;

            // Pause: enabled only when busy, changes text/color
            BtnPause.IsEnabled = ec.IsBusy;
            if (ec.IsPaused)
            {
                BtnPause.Content = "▶ ادامه";
                BtnPause.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60));
            }
            else
            {
                BtnPause.Content = "⏸ وقفه";
                BtnPause.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF3, 0x9C, 0x12));
            }

            // Progress + Status
            ProgressBar.Value = ec.Progress;
            StatusText.Text = ec.StatusMessage;
            LogText.Text = ec.LogText;
        }
        catch (Exception ex)
        {
            // Log the error but don't crash
            System.Diagnostics.Debug.WriteLine($"UpdateButtonStates error: {ex.Message}");
        }
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        var ec = ExtractionController.Instance;
        if (ec.StartCommand is ViewModels.AsyncRelayCommand asyncCmd)
            await asyncCmd.ExecuteAsync();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        ExtractionController.Instance.StopCommand.Execute(null);
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        ExtractionController.Instance.PauseResumeCommand.Execute(null);
        UpdateButtonStates();
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        ExtractionController.Instance.LoadLatestCommand.Execute(null);
        UpdateButtonStates();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.IsLoaded && PasswordBox.Password.Length > 0)
            DatabaseService.Instance.SaveSetting("password", PasswordBox.Password);
    }
}
