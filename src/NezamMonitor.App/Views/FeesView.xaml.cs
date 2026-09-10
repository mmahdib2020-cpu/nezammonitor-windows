using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class FeesView : UserControl
{
    public FeesView()
    {
        InitializeComponent();
        DataContext = new FeesViewModel(Services.DatabaseService.Instance);
    }

    private void FeesGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(dg, hit);
        if (cell == null) return;

        var item = cell.DataContext as FeeItem;
        if (item == null) return;

        var menu = new ContextMenu();
        var colHeader = cell.Column.Header.ToString() ?? "";

        string? cellValue = colHeader switch
        {
            "پرونده" => item.CaseNumber, "مالک" => item.Owner, "رشته" => item.Discipline,
            "مرحله" => item.Stage, "وضعیت" => item.PayStatus, "نوع خدمت" => item.ServiceType, _ => null
        };
        string? columnName = cellValue != null ? colHeader switch
        {
            "پرونده" => "CaseNumber", "مالک" => "Owner", "رشته" => "Discipline",
            "مرحله" => "Stage", "وضعیت" => "Status", "نوع خدمت" => "ServiceType", _ => ""
        } : null;

        if (!string.IsNullOrWhiteSpace(cellValue) && !string.IsNullOrEmpty(columnName))
        {
            var filterItem = new MenuItem { Header = $"🔍 فیلتر بر اساس «{cellValue}»" };
            filterItem.Click += (_, _) => (DataContext as FeesViewModel)?.FilterByValueCommand.Execute($"{columnName}:{cellValue}");
            menu.Items.Add(filterItem);

            var removeItem = new MenuItem { Header = $"❌ لغو فیلتر {colHeader}" };
            removeItem.Click += (_, _) => (DataContext as FeesViewModel)?.RemoveFilter(columnName);
            menu.Items.Add(removeItem);

            menu.Items.Add(new Separator());
            var copyItem = new MenuItem { Header = $"📋 کپی «{cellValue}»" };
            copyItem.Click += (_, _) => Clipboard.SetText(cellValue);
            menu.Items.Add(copyItem);
        }

        if (menu.HasItems) menu.IsOpen = true;
    }

    private static DataGridCell? FindDataGridCell(DataGrid dg, DependencyObject? hit)
    {
        while (hit != null && hit != dg)
        {
            if (hit is DataGridCell cell) return cell;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }
}
