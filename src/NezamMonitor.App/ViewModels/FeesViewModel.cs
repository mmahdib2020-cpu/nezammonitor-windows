using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

public sealed class FeesViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "";
    private string _filterStatus = "همه";
    private string _filterDiscipline = "همه";
    private string _filterStage = "همه";
    private string _filterOwner = "همه";
    private string _filterCaseNumber = "همه";
    private string _filterServiceType = "همه";
    private string _filterAmountFrom = "";
    private string _filterAmountTo = "";

    // Summary
    private string _totalAmount = "۰";
    private string _paidAmount = "۰";
    private string _confirmedAmount = "۰";
    private string _pendingAmount = "۰";
    private int _totalCount;
    private int _paidCount;
    private int _confirmedCount;
    private int _pendingCount;

    public ObservableCollection<FeeItem> Fees { get; } = new();
    public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); ApplyFilters(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string FilterStatus { get => _filterStatus; set { SetProperty(ref _filterStatus, value); ApplyFilters(); } }
    public string FilterDiscipline { get => _filterDiscipline; set { SetProperty(ref _filterDiscipline, value); ApplyFilters(); } }
    public string FilterStage { get => _filterStage; set { SetProperty(ref _filterStage, value); ApplyFilters(); } }
    public string FilterOwner { get => _filterOwner; set { SetProperty(ref _filterOwner, value); ApplyFilters(); } }
    public string FilterCaseNumber { get => _filterCaseNumber; set { SetProperty(ref _filterCaseNumber, value); ApplyFilters(); } }
    public string FilterServiceType { get => _filterServiceType; set { SetProperty(ref _filterServiceType, value); ApplyFilters(); } }
    public string FilterAmountFrom { get => _filterAmountFrom; set { SetProperty(ref _filterAmountFrom, value); ApplyFilters(); } }
    public string FilterAmountTo { get => _filterAmountTo; set { SetProperty(ref _filterAmountTo, value); ApplyFilters(); } }

    public string TotalAmount { get => _totalAmount; set => SetProperty(ref _totalAmount, value); }
    public string PaidAmount { get => _paidAmount; set => SetProperty(ref _paidAmount, value); }
    public string ConfirmedAmount { get => _confirmedAmount; set => SetProperty(ref _confirmedAmount, value); }
    public string PendingAmount { get => _pendingAmount; set => SetProperty(ref _pendingAmount, value); }
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
    public int PaidCount { get => _paidCount; set => SetProperty(ref _paidCount, value); }
    public int ConfirmedCount { get => _confirmedCount; set => SetProperty(ref _confirmedCount, value); }
    public int PendingCount { get => _pendingCount; set => SetProperty(ref _pendingCount, value); }

    private int _filteredCount;
    public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
    public string CountDisplay => FilteredCount == TotalCount
        ? $"{TotalCount} مورد"
        : $"{FilteredCount} از {TotalCount} مورد";

    public bool HasActiveFilters => FilterStatus != "همه" || FilterDiscipline != "همه" || FilterStage != "همه" ||
                                     FilterOwner != "همه" || FilterCaseNumber != "همه" || FilterServiceType != "همه" ||
                                     !string.IsNullOrWhiteSpace(FilterAmountFrom) || !string.IsNullOrWhiteSpace(FilterAmountTo) ||
                                     !string.IsNullOrWhiteSpace(SearchText);

    // Dynamic filter lists
    public List<string> StatusFilters { get; set; } = new() { "همه" };
    public List<string> DisciplineFilters { get; set; } = new() { "همه" };
    public List<string> StageFilters { get; set; } = new() { "همه" };
    public List<string> OwnerFilters { get; set; } = new() { "همه" };
    public List<string> CaseNumberFilters { get; set; } = new() { "همه" };
    public List<string> ServiceTypeFilters { get; set; } = new() { "همه" };

    public ICommand RefreshCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand FilterByValueCommand { get; }

    private string _selectedAmount = "۰ ریال";
    private int _selectedCount;
    public string SelectedAmount { get => _selectedAmount; set => SetProperty(ref _selectedAmount, value); }
    public int SelectedCount { get => _selectedCount; set => SetProperty(ref _selectedCount, value); }

    private List<FeeItem> _all = new();

    public FeesViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(Load);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        FilterByValueCommand = new RelayCommand<string>(FilterByValue);
        Load();
    }

    private void Load()
    {
        Fees.Clear();
        var snapshotId = _db.GetActiveSnapshotId();
        if (snapshotId == 0) { StatusMessage = "داده‌ای موجود نیست"; return; }
        var cases = _db.LoadCases(snapshotId);
        _all.Clear();
        foreach (var c in cases)
            foreach (var f in c.Fees)
            {
                var item = new FeeItem
                {
                    CaseNumber = c.CaseNumber, Owner = c.Owner,
                    Discipline = f.Discipline, ServiceType = f.ServiceType, AmountType = f.AmountType,
                    Stage = f.Stage, Amount = f.Amount, PayStatus = f.PayStatus,
                    ConfirmStatus = f.ConfirmStatus, StartDate = f.StartDate, EndDate = f.EndDate,
                    Description = f.Description
                };
                item.OnSelectedChanged = UpdateSelectedSummary;
                _all.Add(item);
            }

        // Build dynamic filter lists
        StatusFilters = FilterHelper.ExtractDistinctValues(_all, f => f.PayStatus);
        DisciplineFilters = FilterHelper.ExtractDistinctValues(_all, f => f.Discipline);
        StageFilters = FilterHelper.ExtractDistinctValues(_all, f => f.Stage);
        OwnerFilters = FilterHelper.ExtractDistinctValues(_all, f => f.Owner);
        CaseNumberFilters = FilterHelper.ExtractDistinctValues(_all, f => f.CaseNumber);
        ServiceTypeFilters = FilterHelper.ExtractDistinctValues(_all, f => f.ServiceType);
        OnPropertyChanged(nameof(StatusFilters));
        OnPropertyChanged(nameof(DisciplineFilters));
        OnPropertyChanged(nameof(StageFilters));
        OnPropertyChanged(nameof(OwnerFilters));
        OnPropertyChanged(nameof(CaseNumberFilters));
        OnPropertyChanged(nameof(ServiceTypeFilters));

        TotalCount = _all.Count;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = FilterHelper.ApplyFilters(_all,
            (f => f.PayStatus, FilterStatus),
            (f => f.Discipline, FilterDiscipline),
            (f => f.Stage, FilterStage),
            (f => f.Owner, FilterOwner),
            (f => f.CaseNumber, FilterCaseNumber),
            (f => f.ServiceType, FilterServiceType));

        // Amount range
        if (!string.IsNullOrWhiteSpace(FilterAmountFrom) && long.TryParse(NormalizeDigits(FilterAmountFrom).Replace(",", ""), out var fromAmt))
            filtered = filtered.Where(f => ParseAmount(f.Amount) >= fromAmt);
        if (!string.IsNullOrWhiteSpace(FilterAmountTo) && long.TryParse(NormalizeDigits(FilterAmountTo).Replace(",", ""), out var toAmt))
            filtered = filtered.Where(f => ParseAmount(f.Amount) <= toAmt);

        // General search
        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(f =>
                FilterHelper.Matches(f.Owner, SearchText) ||
                FilterHelper.Matches(f.CaseNumber, SearchText) ||
                FilterHelper.Matches(f.Discipline, SearchText) ||
                FilterHelper.Matches(f.ServiceType, SearchText));

        Fees.Clear();
        foreach (var item in filtered) Fees.Add(item);
        FilteredCount = Fees.Count;
        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(HasActiveFilters));
        CalculateSummary(filtered.ToList());
        StatusMessage = CountDisplay;
    }

    private void SelectAll() { foreach (var item in Fees) item.IsSelected = true; UpdateSelectedSummary(); }
    private void DeselectAll() { foreach (var item in Fees) item.IsSelected = false; UpdateSelectedSummary(); }

    private void ClearFilters()
    {
        SearchText = ""; FilterStatus = "همه"; FilterDiscipline = "همه"; FilterStage = "همه";
        FilterOwner = "همه"; FilterCaseNumber = "همه"; FilterServiceType = "همه";
        FilterAmountFrom = ""; FilterAmountTo = "";
    }

    private void FilterByValue(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return;
        var parts = parameter.Split(':', 2);
        if (parts.Length != 2) return;
        switch (parts[0])
        {
            case "Discipline": FilterDiscipline = parts[1]; break;
            case "Owner": FilterOwner = parts[1]; break;
            case "CaseNumber": FilterCaseNumber = parts[1]; break;
            case "Stage": FilterStage = parts[1]; break;
            case "Status": FilterStatus = parts[1]; break;
            case "ServiceType": FilterServiceType = parts[1]; break;
        }
    }

    public void RemoveFilter(string column)
    {
        switch (column)
        {
            case "Discipline": FilterDiscipline = "همه"; break;
            case "Owner": FilterOwner = "همه"; break;
            case "CaseNumber": FilterCaseNumber = "همه"; break;
            case "Stage": FilterStage = "همه"; break;
            case "Status": FilterStatus = "همه"; break;
            case "ServiceType": FilterServiceType = "همه"; break;
        }
    }

    public void UpdateSelectedSummary()
    {
        var selected = Fees.Where(f => f.IsSelected).ToList();
        SelectedCount = selected.Count;
        long total = 0;
        foreach (var item in selected) total += ParseAmount(item.Amount);
        SelectedAmount = FormatAmount(total);
    }

    private void CalculateSummary(List<FeeItem> items)
    {
        long total = 0, paid = 0, confirmed = 0, pending = 0;
        int totalN = 0, paidN = 0, confirmedN = 0, pendingN = 0;
        foreach (var item in items)
        {
            var amount = ParseAmount(item.Amount);
            total += amount; totalN++;
            if (item.PayStatus.Contains("پرداخت")) { paid += amount; paidN++; }
            else if (item.PayStatus.Contains("تایید") || item.ConfirmStatus.Contains("تایید")) { confirmed += amount; confirmedN++; }
            else { pending += amount; pendingN++; }
        }
        TotalAmount = FormatAmount(total); PaidAmount = FormatAmount(paid);
        ConfirmedAmount = FormatAmount(confirmed); PendingAmount = FormatAmount(pending);
        TotalCount = totalN; PaidCount = paidN; ConfirmedCount = confirmedN; PendingCount = pendingN;
    }

    private static long ParseAmount(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return 0;
        var cleaned = amount.Replace("ریال", "").Replace(",", "").Replace(" ", "").Trim();
        cleaned = NormalizeDigits(cleaned);
        return long.TryParse(cleaned, out var result) ? result : 0;
    }

    private static string NormalizeDigits(string input) =>
        input.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4")
             .Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
             .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4")
             .Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");

    private static string FormatAmount(long amount) => amount.ToString("N0") + " ریال";
}

public sealed class FeeItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public Action? OnSelectedChanged;
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string AmountType { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Amount { get; set; } = "";
    public string PayStatus { get; set; } = "";
    public string ConfirmStatus { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Description { get; set; } = "";
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); OnSelectedChanged?.Invoke(); } }
}
