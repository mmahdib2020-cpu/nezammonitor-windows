using System.Windows;
using System.Windows.Controls;
using NezamMonitor.App.ViewModels;
using NezamMonitor.App.Services;

namespace NezamMonitor.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly NavigationService _nav;
    private readonly ThemeManager _themeManager;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _nav = App.Navigation;
        _nav.SetFrame(MainFrame);

        _themeManager = new ThemeManager(DatabaseService.Instance);
        _themeManager.LoadTheme();
        _themeManager.ApplyTheme(_themeManager.CurrentTheme);

        // ApplyTheme can't find this window in app.Windows yet (still constructing),
        // so we must set FlowDirection explicitly here.
        var dir = _themeManager.CurrentTheme.LayoutDirection;
        this.FlowDirection = dir.MainWindowDirection == "LeftToRight"
            ? System.Windows.FlowDirection.LeftToRight
            : System.Windows.FlowDirection.RightToLeft;

        _nav.Register("dashboard", () => new Views.DashboardView());
        _nav.Register("cases", () => new Views.CasesView());
        _nav.Register("update", () => new Views.UpdateView());
        _nav.Register("changes", () => new Views.ChangesView());
        _nav.Register("engineers", () => new Views.EngineersView());
        _nav.Register("fees", () => new Views.FeesView());
        _nav.Register("reports", () => new Views.ReportsView());
        _nav.Register("generator", () => new Views.GeneratorView());
        _nav.Register("excel", () => new Views.ExcelExportView());
        _nav.Register("history", () => new Views.HistoryView());
                _nav.Register("followup", () => new Views.FollowUpView());
                        _nav.Register("androidexport", () => new Views.AndroidExportView());
                        _nav.Register("settings", () => new Views.SettingsView());
        _nav.NavigateTo("dashboard");
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _vm.CurrentView = tag;
            _nav.NavigateTo(tag);
        }
    }
}
