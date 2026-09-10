using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "";
    private string _filterReportType = "همه";
    private string _filterStage = "همه";
    private string _filterOwner = "همه";
    private string _filterCaseNumber = "همه";
    private string _filterEngineer = "همه";
    private string _filterDiscipline = "همه";

    public ObservableCollection<ReportItem> Reports { get; } = new();
    public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); ApplyFilters(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string FilterReportType { get => _filterReportType; set { SetProperty(ref _filterReportType, value); ApplyFilters(); } }
    public string FilterStage { get => _filterStage; set { SetProperty(ref _filterStage, value); ApplyFilters(); } }
    public string FilterOwner { get => _filterOwner; set { SetProperty(ref _filterOwner, value); ApplyFilters(); } }
    public string FilterCaseNumber { get => _filterCaseNumber; set { SetProperty(ref _filterCaseNumber, value); ApplyFilters(); } }
    public string FilterEngineer { get => _filterEngineer; set { SetProperty(ref _filterEngineer, value); ApplyFilters(); } }
    public string FilterDiscipline { get => _filterDiscipline; set { SetProperty(ref _filterDiscipline, value); ApplyFilters(); } }

    public List<string> ReportTypeFilters { get; set; } = new() { "همه" };
    public List<string> StageFilters { get; set; } = new() { "همه" };
    public List<string> OwnerFilters { get; set; } = new() { "همه" };
    public List<string> CaseNumberFilters { get; set; } = new() { "همه" };
    public List<string> EngineerFilters { get; set; } = new() { "همه" };
    public List<string> DisciplineFilters { get; set; } = new() { "همه" };

    private int _totalCount;
    private int _filteredCount;
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
    public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
    public string CountDisplay => FilteredCount == TotalCount
        ? $"{TotalCount} مورد"
        : $"{FilteredCount} از {TotalCount} مورد";

    public bool HasActiveFilters => FilterReportType != "همه" || FilterStage != "همه" || FilterOwner != "همه" ||
                                     FilterCaseNumber != "همه" || FilterEngineer != "همه" || FilterDiscipline != "همه" ||
                                     !string.IsNullOrWhiteSpace(SearchText);

    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand FilterByValueCommand { get; }

    private List<ReportItem> _all = new();

    public ReportsViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(Load);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        FilterByValueCommand = new RelayCommand<string>(FilterByValue);
        Load();
    }

    private void Load()
    {
        Reports.Clear();
        var snapshotId = _db.GetActiveSnapshotId();
        if (snapshotId == 0) { StatusMessage = "داده‌ای موجود نیست"; return; }
        var cases = _db.LoadCases(snapshotId);
        _all.Clear();
        foreach (var c in cases)
            foreach (var r in c.Reports)
                _all.Add(new ReportItem { CaseNumber = c.CaseNumber, Owner = c.Owner, ReportType = r.ReportType, Stage = r.Stage, Engineer = r.Engineer, Discipline = r.Discipline, VisitDate = r.VisitDate });

        ReportTypeFilters = FilterHelper.ExtractDistinctValues(_all, r => r.ReportType);
        StageFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Stage);
        OwnerFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Owner);
        CaseNumberFilters = FilterHelper.ExtractDistinctValues(_all, r => r.CaseNumber);
        EngineerFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Engineer);
        DisciplineFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Discipline);
        OnPropertyChanged(nameof(ReportTypeFilters));
        OnPropertyChanged(nameof(StageFilters));
        OnPropertyChanged(nameof(OwnerFilters));
        OnPropertyChanged(nameof(CaseNumberFilters));
        OnPropertyChanged(nameof(EngineerFilters));
        OnPropertyChanged(nameof(DisciplineFilters));

        TotalCount = _all.Count;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = FilterHelper.ApplyFilters(_all,
            (r => r.ReportType, FilterReportType),
            (r => r.Stage, FilterStage),
            (r => r.Owner, FilterOwner),
            (r => r.CaseNumber, FilterCaseNumber),
            (r => r.Engineer, FilterEngineer),
            (r => r.Discipline, FilterDiscipline));

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(r =>
                FilterHelper.Matches(r.Owner, SearchText) ||
                FilterHelper.Matches(r.CaseNumber, SearchText) ||
                FilterHelper.Matches(r.ReportType, SearchText) ||
                FilterHelper.Matches(r.Engineer, SearchText));

        Reports.Clear();
        foreach (var item in filtered) Reports.Add(item);
        FilteredCount = Reports.Count;
        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(HasActiveFilters));
        StatusMessage = CountDisplay;
    }

    private void ClearFilters()
    {
        SearchText = ""; FilterReportType = "همه"; FilterStage = "همه";
        FilterOwner = "همه"; FilterCaseNumber = "همه"; FilterEngineer = "همه"; FilterDiscipline = "همه";
    }

    private void FilterByValue(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return;
        var parts = parameter.Split(':', 2);
        if (parts.Length != 2) return;
        switch (parts[0])
        {
            case "ReportType": FilterReportType = parts[1]; break;
            case "Stage": FilterStage = parts[1]; break;
            case "Owner": FilterOwner = parts[1]; break;
            case "CaseNumber": FilterCaseNumber = parts[1]; break;
            case "Engineer": FilterEngineer = parts[1]; break;
            case "Discipline": FilterDiscipline = parts[1]; break;
        }
    }

    public void RemoveFilter(string column)
    {
        switch (column)
        {
            case "ReportType": FilterReportType = "همه"; break;
            case "Stage": FilterStage = "همه"; break;
            case "Owner": FilterOwner = "همه"; break;
            case "CaseNumber": FilterCaseNumber = "همه"; break;
            case "Engineer": FilterEngineer = "همه"; break;
            case "Discipline": FilterDiscipline = "همه"; break;
        }
    }
}

public sealed class ReportItem
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Engineer { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string VisitDate { get; set; } = "";
}
