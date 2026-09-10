using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

/// <summary>
/// صفحه پیگیری - نمایش اطلاعات پرونده‌ها با تعداد گزارش‌های ثبت شده
/// </summary>
public sealed class FollowUpViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _filterOwner = "همه";
    private string _filterCaseNumber = "همه";
    private bool _groupByRange = false;

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilters(); }
    }

    public string FilterOwner
    {
        get => _filterOwner;
        set { if (SetProperty(ref _filterOwner, value)) ApplyFilters(); }
    }

    public string FilterCaseNumber
    {
        get => _filterCaseNumber;
        set { if (SetProperty(ref _filterCaseNumber, value)) ApplyFilters(); }
    }

    public bool GroupByRange
    {
        get => _groupByRange;
        set { if (SetProperty(ref _groupByRange, value)) ApplyFilters(); }
    }

    public ObservableCollection<FollowUpItem> Items { get; } = new();
    public ObservableCollection<string> DistinctOwners { get; } = new();
    public ObservableCollection<string> DistinctCaseNumbers { get; } = new();

    public string CountDisplay => $"تعداد: {Items.Count} مورد";

    public ICommand RefreshCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand GroupByRangeCommand { get; }

    public FollowUpViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(() => { SaveEdits(); LoadData(); });
        SaveCommand = new RelayCommand(ExecuteSave);
        GroupByRangeCommand = new RelayCommand(() => { GroupByRange = !GroupByRange; });
        LoadData();
    }

    private void LoadData()
    {
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        
        // دریافت ویرایش‌های ذخیره شده
        var savedEdits = _db.GetAllFollowUpEdits();

        // شمارش تعداد گزارش‌ها بر اساس تعداد ردیف گزارش‌های ثبت شده در هر پرونده
        var reportCounts = new Dictionary<string, int>();
        foreach (var c in cases)
        {
            reportCounts[c.CaseNumber] = c.Reports.Count;
        }

        DistinctOwners.Clear();
        DistinctOwners.Add("همه");
        DistinctCaseNumbers.Clear();
        DistinctCaseNumbers.Add("همه");

        var allItems = new List<FollowUpItem>();
        int row = 1;
        foreach (var c in cases)
        {
            var reportCount = reportCounts.TryGetValue(c.CaseNumber, out var cnt) ? cnt : 0;
            
            // بررسی وجود ویرایش ذخیره شده
            var hasEdit = savedEdits.TryGetValue(c.CaseNumber, out var edit);

            var item = new FollowUpItem
            {
                RowId = row++,
                CaseNumber = hasEdit && !string.IsNullOrEmpty(edit.CaseNumberEdit) ? edit.CaseNumberEdit : c.CaseNumber,
                Owner = hasEdit && !string.IsNullOrEmpty(edit.OwnerEdit) ? edit.OwnerEdit : c.Owner,
                Address = hasEdit && !string.IsNullOrEmpty(edit.AddressEdit) ? edit.AddressEdit : (c.Specification?.Address ?? ""),
                OwnerMobile = hasEdit && !string.IsNullOrEmpty(edit.OwnerMobileEdit) ? edit.OwnerMobileEdit : c.OwnerMobile,
                ReportCount = reportCount,
                Description = hasEdit ? edit.Description : "",
                // مقادیر اصلی برای تشخیص تغییر (همیشه از دیتابیس)
                OriginalCaseNumber = c.CaseNumber,
                OriginalOwner = c.Owner,
                OriginalAddress = c.Specification?.Address ?? "",
                OriginalOwnerMobile = c.OwnerMobile
            };
            allItems.Add(item);
        }

        // استخراج مقادیر منحصربفرد
        foreach (var owner in allItems.Select(i => i.Owner).Distinct().OrderBy(o => o))
            DistinctOwners.Add(owner);
        foreach (var cn in allItems.Select(i => i.CaseNumber).Distinct().OrderBy(c => c))
            DistinctCaseNumbers.Add(cn);

        // ذخیره لیست کامل
        _allItems = allItems;
        ApplyFilters();
    }

    private List<FollowUpItem> _allItems = new();

    private void ApplyFilters()
    {
        Items.Clear();
        var filtered = _allItems.AsEnumerable();

        // فیلتر متن
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var normSearch = FilterHelper.Normalize(SearchText);
            filtered = filtered.Where(i =>
                FilterHelper.Normalize($"{i.CaseNumber} {i.Owner} {i.Address} {i.OwnerMobile}")
                    .Contains(normSearch, StringComparison.OrdinalIgnoreCase));
        }

        // فیلتر مالک
        if (FilterOwner != "همه")
        {
            filtered = filtered.Where(i => FilterHelper.Matches(i.Owner, FilterOwner));
        }

        // فیلتر شماره پرونده
        if (FilterCaseNumber != "همه")
        {
            filtered = filtered.Where(i => FilterHelper.Matches(i.CaseNumber, FilterCaseNumber));
        }

        // دسته‌بندی بر اساس محدوده
        if (GroupByRange)
        {
            filtered = filtered.OrderBy(i => ExtractRange(i.Address))
                               .ThenBy(i => i.Owner);
        }
        else
        {
            filtered = filtered.OrderBy(i => i.RowId);
        }

        foreach (var item in filtered)
            Items.Add(item);

        OnPropertyChanged(nameof(CountDisplay));
    }

    /// <summary>
    /// استخراج محدوده از آدرس برای دسته‌بندی
    /// </summary>
    private static string ExtractRange(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "نامشخص";

        // استخراج کلمه کلیدی از آدرس برای دسته‌بندی
        // مثال: "خیابان امام، کوچه 5" → "خیابان امام"
        var parts = address.Split(new[] { "،", ",", "خیابان", "کوچه", "بلوار", "جاده" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
            return parts[0].Trim();
        return address.Length > 20 ? address[..20] : address;
    }

    /// <summary>
    /// ذخیره ویرایش‌ها در دیتابیس
    /// </summary>
    public void SaveEdits()
    {
        foreach (var item in _allItems)
        {
            _db.SaveFollowUpEdit(
                item.OriginalCaseNumber,
                item.Description,
                item.CaseNumber != item.OriginalCaseNumber ? item.CaseNumber : "",
                item.Owner != item.OriginalOwner ? item.Owner : "",
                item.Address != item.OriginalAddress ? item.Address : "",
                item.OwnerMobile != item.OriginalOwnerMobile ? item.OwnerMobile : ""
            );
        }
    }

    /// <summary>
    /// اجرای ذخیره صریح با تایید موفقیت
    /// </summary>
    private void ExecuteSave()
    {
        try
        {
            SaveEdits();

            // Read-back verification
            var savedEdits = _db.GetAllFollowUpEdits();
            bool allVerified = true;
            foreach (var item in _allItems)
            {
                if (savedEdits.TryGetValue(item.OriginalCaseNumber, out var edit))
                {
                    // بررسی توضیحات
                    if (item.Description != "" && edit.Description != item.Description)
                        allVerified = false;
                    // بررسی ویرایش‌های دیگر
                    if (item.CaseNumber != item.OriginalCaseNumber && edit.CaseNumberEdit != item.CaseNumber)
                        allVerified = false;
                    if (item.Owner != item.OriginalOwner && edit.OwnerEdit != item.Owner)
                        allVerified = false;
                    if (item.Address != item.OriginalAddress && edit.AddressEdit != item.Address)
                        allVerified = false;
                    if (item.OwnerMobile != item.OriginalOwnerMobile && edit.OwnerMobileEdit != item.OwnerMobile)
                        allVerified = false;
                }
            }

            if (allVerified)
                MessageBox.Show("✅ تغییرات با موفقیت ذخیره شد.", "ذخیره", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("❌ ذخیره تغییرات ناموفق بود. لطفاً دوباره تلاش کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ خطا در ذخیره: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// بروزرسانی یک سلول از داده‌های پرونده‌ها (فقط از طریق راست کلیک)
    /// </summary>
    public void RefreshCellFromCases(FollowUpItem item, string propertyName)
    {
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var c = cases.FirstOrDefault(x => x.CaseNumber == item.OriginalCaseNumber);
        if (c == null) return;

        switch (propertyName)
        {
            case nameof(FollowUpItem.CaseNumber):
                item.CaseNumber = c.CaseNumber;
                break;
            case nameof(FollowUpItem.Owner):
                item.Owner = c.Owner;
                break;
            case nameof(FollowUpItem.Address):
                item.Address = c.Specification?.Address ?? "";
                break;
            case nameof(FollowUpItem.OwnerMobile):
                item.OwnerMobile = c.OwnerMobile;
                break;
        }
        
        // ذخیره تغییرات
        SaveEdits();
    }
}

/// <summary>
/// آیتم پیگیری
/// </summary>
public class FollowUpItem : INotifyPropertyChanged
{
    private string _caseNumber = "";
    private string _owner = "";
    private string _address = "";
    private string _ownerMobile = "";
    private string _description = "";

    public int RowId { get; set; }

    public string CaseNumber
    {
        get => _caseNumber;
        set { _caseNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCaseNumberModified)); }
    }

    public string Owner
    {
        get => _owner;
        set { _owner = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOwnerModified)); }
    }

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAddressModified)); }
    }

    public string OwnerMobile
    {
        get => _ownerMobile;
        set { _ownerMobile = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOwnerMobileModified)); }
    }

    /// <summary>
    /// توضیحات (قابل ویرایش، ذخیره می‌شود)
    /// </summary>
    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public int ReportCount { get; set; }

    // مقادیر اصلی از پرونده‌ها
    public string OriginalCaseNumber { get; set; } = "";
    public string OriginalOwner { get; set; } = "";
    public string OriginalAddress { get; set; } = "";
    public string OriginalOwnerMobile { get; set; } = "";

    // تشخیص تغییر
    public bool IsCaseNumberModified => FilterHelper.Normalize(CaseNumber) != FilterHelper.Normalize(OriginalCaseNumber);
    public bool IsOwnerModified => FilterHelper.Normalize(Owner) != FilterHelper.Normalize(OriginalOwner);
    public bool IsAddressModified => FilterHelper.Normalize(Address) != FilterHelper.Normalize(OriginalAddress);
    public bool IsOwnerMobileModified => FilterHelper.Normalize(OwnerMobile) != FilterHelper.Normalize(OriginalOwnerMobile);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
