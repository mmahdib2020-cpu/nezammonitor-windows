using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using NezamMonitor.Core;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;
using NezamMonitor.Core.Reports;

namespace NezamMonitor.App.ViewModels;

public sealed class GeneratorViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _templatePath = "";
    private string _outputPath = "";
    private string _statusMessage = "مرحله و تمپلیت را انتخاب کنید";
    private int _progress;
    private string _progressText = "";
    private int _selectedStage = 1;
    private string _selectedTemplateName = "";
    private string _selectedTemplatePath = "";
    private string _reportDate = "";
    private bool _isHistoryExpanded = false;
    private string _historySearchText = "";
    private int _historyFilterStage = 0;
    private int _historyFilterFormat = 0;
    private string _previewText = "";
    private bool _useLtr = false;
    private int _activeTab = 0;

    public string TemplatePath { get => _templatePath; set => SetProperty(ref _templatePath, value); }
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string ProgressText { get => _progressText; set => SetProperty(ref _progressText, value); }

    public int SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (SetProperty(ref _selectedStage, value))
                AutoSelectTemplate();
        }
    }

    public string SelectedTemplateName
    {
        get => _selectedTemplateName;
        set => SetProperty(ref _selectedTemplateName, value);
    }

    public string SelectedTemplatePath
    {
        get => _selectedTemplatePath;
        set => SetProperty(ref _selectedTemplatePath, value);
    }

    public string ReportDate
    {
        get => _reportDate;
        set => SetProperty(ref _reportDate, value);
    }

    public bool IsHistoryExpanded
    {
        get => _isHistoryExpanded;
        set => SetProperty(ref _isHistoryExpanded, value);
    }

    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            if (SetProperty(ref _historySearchText, value))
                LoadHistory();
        }
    }

    public int HistoryFilterStage
    {
        get => _historyFilterStage;
        set
        {
            if (SetProperty(ref _historyFilterStage, value))
                LoadHistory();
        }
    }

    public int HistoryFilterFormat
    {
        get => _historyFilterFormat;
        set
        {
            if (SetProperty(ref _historyFilterFormat, value))
                LoadHistory();
        }
    }

    public string PreviewText { get => _previewText; set => SetProperty(ref _previewText, value); }
    public bool UseLtr { get => _useLtr; set => SetProperty(ref _useLtr, value); }
    public int ActiveTab { get => _activeTab; set => SetProperty(ref _activeTab, value); }

    public ObservableCollection<CaseReportItem> ReportStatus { get; } = new();
    public ObservableCollection<TemplateInfo> AvailableTemplates { get; } = new();
    public ObservableCollection<string> TemplateList
    {
        get
        {
            var list = new ObservableCollection<string>(AvailableTemplates.Select(t => t.FileName));
            return list;
        }
    }

    public string CountDisplay => $"تعداد: {ReportStatus.Count(n => n.IsSelected)} از {ReportStatus.Count}";

    public ICommand BrowseTemplateCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ToggleHistoryCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand DeleteReportCommand { get; }
    public ICommand SwitchToGenerateCommand { get; }
    public ICommand SwitchToHistoryCommand { get; }
    public ICommand DeleteLogCommand { get; }
    public ICommand SyncOutputCommand { get; }

    public bool HasActiveFilters => !string.IsNullOrEmpty(HistorySearchText) || HistoryFilterStage > 0 || HistoryFilterFormat > 0;

    public GeneratorViewModel(NezamDatabase db)
    {
        _db = db;
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        _templatePath = Path.Combine(exeDir, "templates");
        _outputPath = Path.Combine(exeDir, "outputs");
        _reportDate = PersianDateHelper.ToPersianDateDigits(DateTime.Now);

        BrowseTemplateCommand = new RelayCommand(BrowseTemplate);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        ScanCommand = new RelayCommand(Scan);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        GenerateCommand = new RelayCommand(Generate);
        PreviewCommand = new RelayCommand(Preview);
        ToggleHistoryCommand = new RelayCommand(() => IsHistoryExpanded = !IsHistoryExpanded);
        RefreshHistoryCommand = new RelayCommand(LoadHistory);
        OpenReportCommand = new RelayCommand<ReportHistoryItem>(OpenReport);
        OpenFolderCommand = new RelayCommand<ReportHistoryItem>(OpenFolder);
        RegenerateCommand = new RelayCommand<ReportHistoryItem>(RegenerateWithConfirmation);
        DeleteReportCommand = new RelayCommand<ReportHistoryItem>(DeleteReportWithConfirmation);
        SwitchToGenerateCommand = new RelayCommand(() => ActiveTab = 0);
        SwitchToHistoryCommand = new RelayCommand(() => { ActiveTab = 1; LoadHistory(); });
        DeleteLogCommand = new RelayCommand<ReportHistoryItem>(DeleteLog);
        SyncOutputCommand = new RelayCommand(SyncOutputFolder);

        LoadTemplates();
        LoadHistory();
    }

    private void LoadTemplates()
    {
        AvailableTemplates.Clear();
        var generator = new StageReportGenerator(_templatePath);
        foreach (var t in generator.ListTemplates())
            AvailableTemplates.Add(t);
        AutoSelectTemplate();
    }

    private void AutoSelectTemplate()
    {
        var match = AvailableTemplates.FirstOrDefault(t => t.Stage == SelectedStage);
        if (match != null)
        {
            SelectedTemplateName = match.FileName;
            SelectedTemplatePath = match.Path;
        }
        else
        {
            SelectedTemplateName = "";
            SelectedTemplatePath = "";
        }
    }

    private void BrowseTemplate()
    {
        var dialog = new OpenFileDialog
        {
            Title = "انتخاب تمپلیت",
            Filter = "فایل Word|*.docx",
            InitialDirectory = _templatePath
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedTemplatePath = dialog.FileName;
        }
    }

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "انتخاب پوشه خروجی",
            InitialDirectory = _outputPath
        };
        if (dialog.ShowDialog() == true)
        {
            _outputPath = dialog.FolderName;
        }
    }

    /// <summary>
    /// اسکن پرونده‌ها و نمایش وضعیت واقعی گزارش‌ها
    /// </summary>
    private void Scan()
    {
        try
        {
            ReportStatus.Clear();
            Directory.CreateDirectory(_outputPath);
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            var stages = new[] { 1, 2, 3 };
            
            // دریافت تمام گزارش‌های تولید شده برای بررسی وضعیت
            var existingReports = _db.GetGeneratedReports();
            var existingReportSet = new HashSet<string>(
                existingReports.Select(r => $"{r.CaseNumber}|{r.Stage}"));
            
            int current = 0;
            foreach (var c in cases)
            {
                current++;
                Progress = (int)((double)current / cases.Count * 100);
                ProgressText = $"{current}/{cases.Count}";
                
                foreach (var stage in stages)
                {
                    var reportKey = $"{c.CaseNumber}|{stage}";
                    var hasReport = existingReportSet.Contains(reportKey);
                    
                    var item = new CaseReportItem
                    {
                        CaseNumber = c.CaseNumber,
                        RowNumber = GetRowNumber(c.CaseNumber),
                        Owner = c.Owner,
                        FullAddress = c.Specification?.Address ?? "",
                        Companion = c.OwnerMobile,
                        Stage = stage,
                        StageName = StageReportRules.StageName(stage),
                        Status = hasReport ? "✅ تولید شده" : "⏳ در انتظار",
                    };
                    ReportStatus.Add(item);
                }
            }
            Progress = 100;
            StatusMessage = $"اسکن انجام شد: {ReportStatus.Count} مورد یافت شد";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
            Progress = 0;
        }
    }

    private int GetRowNumber(string caseNumber)
    {
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var idx = cases.FindIndex(c => c.CaseNumber == caseNumber);
        return idx >= 0 ? idx + 1 : 1;
    }

    private void SelectAll()
    {
        foreach (var item in ReportStatus)
            item.IsSelected = true;
    }

    private void DeselectAll()
    {
        foreach (var item in ReportStatus)
            item.IsSelected = false;
    }

    private void Preview()
    {
        var selected = ReportStatus.Where(r => r.IsSelected && !r.Status.Contains("تولید شده")).ToList();
        if (selected.Count == 0)
        {
            PreviewText = "موردی انتخاب نشده";
            return;
        }

        var ltrSuffix = UseLtr ? " - LTR" : "";
        var lines = new List<string> { $"گزارش‌های آماده تولید ({selected.Count} مورد):", "" };

        foreach (var item in selected)
        {
            var folderName = StageReportRules.GenerateFolderName(item.RowNumber, new Case { CaseNumber = item.CaseNumber, Owner = item.Owner });
            lines.Add($"- {item.Owner} ({item.StageName}), {item.CaseNumber} -> {folderName}{ltrSuffix}/گزارش مرحله {item.Stage}{ltrSuffix}.docx");
        }

        PreviewText = string.Join("\r\n", lines);
    }

    /// <summary>
    /// تولید گزارش‌های انتخاب شده
    /// </summary>
    private void Generate()
    {
        try
        {
            if (string.IsNullOrEmpty(SelectedTemplatePath) || !File.Exists(SelectedTemplatePath))
            {
                StatusMessage = "تمپلیت انتخاب نشده یا فایل وجود ندارد";
                return;
            }

            Directory.CreateDirectory(_outputPath);
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            var generator = new StageReportGenerator(Path.GetDirectoryName(SelectedTemplatePath) ?? _templatePath);

            int generated = 0, failed = 0, skipped = 0;
            var selected = ReportStatus.Where(r => r.IsSelected).ToList();
            int total = selected.Count;

            if (total == 0)
            {
                StatusMessage = "هیچ موردی انتخاب نشده";
                return;
            }

            int current = 0;
            foreach (var item in selected)
            {
                current++;
                Progress = (int)((double)current / total * 100);
                ProgressText = $"{current}/{total}";

                var c = cases.FirstOrDefault(x => x.CaseNumber == item.CaseNumber);
                if (c == null) { failed++; item.Status = "❌ پرونده یافت نشد"; continue; }

                // بررسی تکراری بودن گزارش
                if (GeneratorHelpers.IsReportGenerateDuplicate(c.CaseNumber, item.Stage, DateTime.Now, UseLtr ? "LTR" : "Persian"))
                {
                    var confirmMsg = $"گزارش مرحله {item.StageName} برای پرونده {c.Owner} قبلاً تولید شده است.\n\nآیا می‌خواهید گزارش را مجدداً تولید کنید؟";
                    var title = "تکراری بودن گزارش";

                    var dlgResult = MessageBox.Show(confirmMsg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult != MessageBoxResult.Yes)
                    {
                        item.Status = "❌ لغو (تکراری)";
                        skipped++;
                        continue;
                    }
                }

                var genResult = generator.GenerateReport(c, item.Stage, item.RowNumber, _outputPath, DateTime.Now, SelectedTemplatePath, UseLtr);

                if (genResult.Success && genResult.OutputPath != null && File.Exists(genResult.OutputPath) && new FileInfo(genResult.OutputPath).Length > 0)
                {
                    var numFormat = UseLtr ? "LTR" : "Persian";
                    _db.SaveGeneratedReport(c.CaseNumber, item.Stage, SelectedTemplateName, genResult.OutputPath, c.Owner, numFormat);
                    generated++;
                    item.Status = "✅ تولید شده";
                    item.IsSelected = false;
                }
                else if (genResult.Success)
                {
                    failed++;
                    item.Status = "❌ فایل ایجاد نشد";
                }
                else
                {
                    failed++;
                    item.Status = $"❌ {genResult.ErrorMessage}";
                }
            }

            StatusMessage = $"تکمیل: {generated} تولید شد | {failed} خطا | {skipped} لغو شد | مسیر: {_outputPath}";
            Progress = 100;
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }

    /// <summary>
    /// بارگذاری تاریخچه گزارش‌های تولید شده
    /// </summary>
    public void LoadHistory()
    {
        ReportHistory.Clear();
        var reports = _db.GetGeneratedReports();
        int row = 1;
        foreach (var r in reports)
        {
            if (HistoryFilterStage > 0 && r.Stage != HistoryFilterStage) continue;
            if (HistoryFilterFormat > 0)
            {
                var fmtFilter = HistoryFilterFormat == 1 ? "Persian" : (HistoryFilterFormat == 2 ? "LTR" : "English");
                if (r.NumberFormat != fmtFilter) continue;
            }
            if (!string.IsNullOrEmpty(HistorySearchText))
            {
                var normSearch = FilterHelper.Normalize(HistorySearchText);
                var normStored = FilterHelper.Normalize($"{r.OwnerName} {r.CaseNumber} {r.NumberFormat}");
                if (!normStored.Contains(normSearch, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var fileExists = !string.IsNullOrEmpty(r.OutputPath) && File.Exists(r.OutputPath);
            
            // استخراج مسیر پوشه از OutputPath اگر FolderName خالی باشد
            var folderPath = !string.IsNullOrEmpty(r.FolderName) ? r.FolderName : Path.GetDirectoryName(r.OutputPath);
            
            // نمایش زمان تولید گزارش
            var displayTime = FormatTimestamp(r.CreatedAt);

            ReportHistory.Add(new ReportHistoryItem
            {
                RowId = row++,
                CaseNumber = r.CaseNumber,
                OwnerName = r.OwnerName,
                Owner = r.OwnerName,
                Stage = r.Stage,
                StageName = StageReportRules.StageName(r.Stage),
                ReportType = StageReportRules.StageName(r.Stage),
                ReportDate = displayTime,
                NumberFormat = r.NumberFormat,
                OutputPath = r.OutputPath,
                Status = fileExists ? "✅ موجود" : "⚠️ فایل موجود نیست",
                FileExists = fileExists,
                FolderPath = folderPath
            });
        }
    }

    /// <summary>
    /// فرمت‌بندی زمان تولید گزارش برای نمایش
    /// </summary>
    private string FormatTimestamp(string? createdAt)
    {
        if (string.IsNullOrEmpty(createdAt))
            return "نامشخص";
        
        // اگر فرمت فارسی باشد (1405-04-01T00:00:00)
        if (createdAt.Contains('-') && createdAt.Length >= 10)
        {
            // استخراج تاریخ فارسی
            var datePart = createdAt.Split('T')[0];
            return datePart.Replace('-', '/');
        }
        
        // اگر فرمت میلادی باشد (2026/09/07 07:23)
        if (createdAt.Contains('/'))
        {
            return createdAt;
        }
        
        return createdAt;
    }

    public ObservableCollection<ReportHistoryItem> ReportHistory { get; } = new();

    /// <summary>
    /// باز کردن فایل گزارش
    /// </summary>
    public void OpenReport(ReportHistoryItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
        {
            StatusMessage = "فایل یافت نشد";
            return;
        }
        var success = Process.Start(new ProcessStartInfo
        {
            FileName = item.OutputPath,
            UseShellExecute = true
        });
        if (success == null)
            StatusMessage = "خطا در باز کردن فایل";
        else
            StatusMessage = $"باز شد: {item.FileName}";
    }

    /// <summary>
    /// باز کردن پوشه گزارش
    /// </summary>
    public void OpenFolder(ReportHistoryItem? item)
    {
        if (item == null)
        {
            StatusMessage = "موردی انتخاب نشده";
            return;
        }
        
        // استخراج مسیر پوشه از OutputPath اگر FolderPath خالی باشد
        var folderPath = item.FolderPath;
        if (string.IsNullOrEmpty(folderPath) && !string.IsNullOrEmpty(item.OutputPath))
        {
            folderPath = Path.GetDirectoryName(item.OutputPath);
        }
        
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = $"پوشه یافت نشد: {folderPath ?? "نامشخص"}";
            return;
        }
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
            StatusMessage = $"باز شد: {folderPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در باز کردن پوشه: {ex.Message}";
        }
    }

    /// <summary>
    /// باز کردن پوشه پرونده (از طریق context menu میز کار)
    /// </summary>
    public void OpenFolderForCase(CaseReportItem item)
    {
        if (item == null) return;
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var c = cases.FirstOrDefault(x => x.CaseNumber == item.CaseNumber);
        if (c == null) { StatusMessage = "پرونده یافت نشد"; return; }

        var folderName = StageReportRules.GenerateFolderName(item.RowNumber, c);
        var folderPath = Path.Combine(_outputPath, folderName);
        if (Directory.Exists(folderPath))
        {
            Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true });
            StatusMessage = $"باز شد: {folderPath}";
        }
        else
        {
            StatusMessage = $"پوشه وجود ندارد: {folderPath}";
        }
    }

    /// <summary>
    /// حذف فایل و لاگ با تایید کاربر
    /// </summary>
    public void DeleteReportWithConfirmation(ReportHistoryItem? item)
    {
        if (item == null) return;
        if (!item.FileExists && string.IsNullOrEmpty(item.OutputPath)) return;

        var confirmMsg = $"آیا از حذف فایل و لاگ این گزارش اطمینان دارید؟\n\nمالک: {item.OwnerName}\nمرحله: {item.StageName}\nفایل: {item.FileName}\n\nاین عملیات غیرقابل بازگشت است.";
        var confirm = MessageBox.Show(confirmMsg, "تایید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو حذف";
            return;
        }

        try
        {
            if (item.FileExists && !string.IsNullOrEmpty(item.OutputPath))
                File.Delete(item.OutputPath);

            _db.DeleteGeneratedReportByPath(item.OutputPath);

            if (!string.IsNullOrEmpty(item.FolderPath) && Directory.Exists(item.FolderPath))
            {
                if (!Directory.EnumerateFileSystemEntries(item.FolderPath).Any())
                    Directory.Delete(item.FolderPath);
                else
                    StatusMessage = $"فایل و لاگ حذف شد: {item.FileName} (پوشه خالی نیست)";
            }
            else
            {
                StatusMessage = $"فایل و لاگ حذف شد: {item.FileName}";
            }
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در حذف: {ex.Message}";
        }
    }

    /// <summary>
    /// حذف فقط لاگ (بدون حذف فایل)
    /// </summary>
    public void DeleteLog(ReportHistoryItem? item)
    {
        if (item == null) { StatusMessage = "موردی انتخاب نشده"; return; }
        try
        {
            _db.DeleteGeneratedReportByPath(item.OutputPath);
            StatusMessage = $"لاگ حذف شد: {item.FileName} (فایل روی دیسک حذف نشد)";
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در حذف لاگ: {ex.Message}";
        }
    }

    /// <summary>
    /// تولید مجدد با تایید کاربر
    /// </summary>
    public void RegenerateWithConfirmation(ReportHistoryItem? item)
    {
        if (item == null) { StatusMessage = "موردی انتخاب نشده"; return; }

        var confirmMsg = $"این گزارش قبلاً تولید شده است.\n\nآیا می‌خواهید آن را مجدداً تولید کنید؟\n\nمالک: {item.OwnerName}\nمرحله: {item.StageName}";
        var confirm = MessageBox.Show(confirmMsg, "تولید مجدد", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو تولید مجدد";
            return;
        }

        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var c = cases.FirstOrDefault(x => x.CaseNumber == item.CaseNumber);
        if (c == null) { StatusMessage = "پرونده یافت نشد"; return; }

        var idx = cases.IndexOf(c);
        var isLtr = item.NumberFormat == "LTR";
        var generator = new StageReportGenerator(Path.GetDirectoryName(SelectedTemplatePath) ?? _templatePath);
        var genResult = generator.GenerateReport(c, item.Stage, idx + 1, _outputPath, DateTime.Now, null, isLtr);

        if (genResult.Success && genResult.OutputPath != null && File.Exists(genResult.OutputPath))
        {
            _db.DeleteGeneratedReport(c.CaseNumber, item.Stage);
            var numFormat = isLtr ? "LTR" : "Persian";
            _db.SaveGeneratedReport(c.CaseNumber, item.Stage, SelectedTemplateName, genResult.OutputPath, c.Owner, numFormat);
            StatusMessage = $"تولید مجدد: {item.CaseNumber} مرحله {item.StageName} ✅";
            LoadHistory();
        }
        else
        {
            StatusMessage = $"خطا: {genResult.ErrorMessage}";
        }
    }

    public void SyncOutputFolder()
    {
        try
        {
            var result = _db.SyncOutputFolder(_outputPath);
            StatusMessage = $"sync: added={result.added}, removed={result.removed}, existing={result.existing}";
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در سینک: {ex.Message}";
        }
    }
}

/// <summary>
/// آیتم وضعیت تولید گزارش (در جدول اسکن)
/// </summary>
public class CaseReportItem : INotifyPropertyChanged
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string CaseNumber { get; set; } = "";
    public int RowNumber { get; set; }
    public string Owner { get; set; } = "";
    public string FullAddress { get; set; } = "";
    public string Companion { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";
    public string Status { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// آیتم تاریخچه گزارش‌های تولید شده
/// </summary>
public class ReportHistoryItem : INotifyPropertyChanged
{
    public int RowId { get; set; }
    public string CaseNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string ReportDate { get; set; } = "";
    public string NumberFormat { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string FileName => Path.GetFileName(OutputPath)?.Replace("گزارش مرحله ", "").Replace(" - LTR.docx", "") ?? "";
    public string Status { get; set; } = "";
    public bool FileExists { get; set; }
    public string FolderPath { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
