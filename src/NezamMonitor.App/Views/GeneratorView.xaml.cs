using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class GeneratorView : UserControl
{
    public GeneratorView()
    {
        InitializeComponent();
        DataContext = new GeneratorViewModel(DatabaseService.Instance);
    }

    private void StageRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag && int.TryParse(tag, out int stage))
        {
            if (DataContext is GeneratorViewModel vm)
                vm.SelectedStage = stage;
        }
    }

    private void CaseGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is CaseReportItem item)
        {
            var menu = new ContextMenu();

            // تولید گزارش این پرونده
            var genItem = new MenuItem { Header = "📝 تولید گزارش", Tag = item };
            genItem.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                {
                    item.IsSelected = true;
                    vm.GenerateCommand.Execute(null);
                    item.IsSelected = false;
                }
            };
            menu.Items.Add(genItem);

            // باز کردن پوشه پرونده
            var openFolder = new MenuItem { Header = "📂 باز کردن پوشه پرونده", Tag = item };
            openFolder.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.OpenFolderForCase(item);
            };
            menu.Items.Add(openFolder);

            // مشاهده گزارش‌های این پرونده
            var viewReports = new MenuItem { Header = "📚 مشاهده گزارش‌های این پرونده", Tag = item };
            viewReports.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                {
                    vm.HistorySearchText = item.CaseNumber;
                    vm.ActiveTab = 1;
                    vm.LoadHistory();
                }
            };
            menu.Items.Add(viewReports);

            menu.Items.Add(new Separator());

            // کپی شماره پرونده
            var copyCase = new MenuItem { Header = "📋 کپی شماره پرونده", Tag = item };
            copyCase.Click += (s, args) =>
            {
                Clipboard.SetText(item.CaseNumber);
                if (DataContext is GeneratorViewModel vm)
                    vm.StatusMessage = $"شماره پرونده کپی شد: {item.CaseNumber}";
            };
            menu.Items.Add(copyCase);

            // کپی نام مالک
            var copyOwner = new MenuItem { Header = "📋 کپی نام مالک", Tag = item };
            copyOwner.Click += (s, args) =>
            {
                Clipboard.SetText(item.Owner);
                if (DataContext is GeneratorViewModel vm)
                    vm.StatusMessage = $"نام مالک کپی شد: {item.Owner}";
            };
            menu.Items.Add(copyOwner);

            menu.IsOpen = true;
        }
    }

    private void HistoryGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is ReportHistoryItem item)
        {
            var menu = new ContextMenu();

            // باز کردن گزارش
            var openReport = new MenuItem { Header = "📄 باز کردن گزارش", Tag = item };
            openReport.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.OpenReport(item);
            };
            menu.Items.Add(openReport);

            // باز کردن پوشه
            var openFolder = new MenuItem { Header = "📂 باز کردن پوشه", Tag = item };
            openFolder.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.OpenFolder(item);
            };
            menu.Items.Add(openFolder);

            // تولید مجدد (با تایید)
            var regenerate = new MenuItem { Header = "🔄 تولید مجدد", Tag = item };
            regenerate.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.RegenerateWithConfirmation(item);
            };
            menu.Items.Add(regenerate);

            menu.Items.Add(new Separator());

            // حذف لاگ
            var deleteLog = new MenuItem { Header = "🗑 حذف لاگ", Tag = item };
            deleteLog.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.DeleteLog(item);
            };
            menu.Items.Add(deleteLog);

            // حذف فایل و لاگ (با تایید)
            var deleteAll = new MenuItem { Header = "🗑 حذف فایل و لاگ", Tag = item };
            deleteAll.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.DeleteReportWithConfirmation(item);
            };
            menu.Items.Add(deleteAll);

            menu.Items.Add(new Separator());

            // کپی مسیر فایل
            var copyPath = new MenuItem { Header = "📋 کپی مسیر فایل", Tag = item };
            copyPath.Click += (s, args) =>
            {
                Clipboard.SetText(item.OutputPath);
                if (DataContext is GeneratorViewModel vm)
                    vm.StatusMessage = $"مسیر کپی شد: {item.OutputPath}";
            };
            menu.Items.Add(copyPath);

            menu.IsOpen = true;
        }
    }
}
