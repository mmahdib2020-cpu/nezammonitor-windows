using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class CasesView : UserControl
{
    private CasesViewModel _vm;

    public CasesView()
    {
        InitializeComponent();
        _vm = new CasesViewModel(DatabaseService.Instance);
        DataContext = _vm;

        // Scroll to end (RTL first columns: row#, case#) after layout is ready
        Loaded += (_, _) =>
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                var sv = FindChild<ScrollViewer>(CasesGrid);
                if (sv != null) sv.ScrollToEnd();
            });
        };
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CasesViewModel.Cases))
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                {
                    var sv = FindChild<ScrollViewer>(CasesGrid);
                    if (sv != null) sv.ScrollToEnd();
                });
        };
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void CasesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CasesGrid.SelectedItem is CaseDisplayItem selectedItem)
            _vm.ShowCaseDetails(selectedItem);
    }
}
