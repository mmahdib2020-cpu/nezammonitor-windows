using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

public sealed class EngineersViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "";
    private string _filterDiscipline = "همه";
    private string _filterOwner = "همه";
    private string _filterCaseNumber = "همه";
    private string _filterEngineer = "همه";

    public ObservableCollection<EngineerItem> Engineers { get; } = new();
    public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); ApplyFilters(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string FilterDiscipline { get => _filterDiscipline; set { SetProperty(ref _filterDiscipline, value); ApplyFilters(); } }
    public string FilterOwner { get => _filterOwner; set { SetProperty(ref _filterOwner, value); ApplyFilters(); } }
    public string FilterCaseNumber { get => _filterCaseNumber; set { SetProperty(ref _filterCaseNumber, value); ApplyFilters(); } }
    public string FilterEngineer { get => _filterEngineer; set { SetProperty(ref _filterEngineer, value); ApplyFilters(); } }

    // Dynamic dropdown values from actual data
    public List<string> DisciplineFilters { get; set; } = new() { "همه" };
    public List<string> OwnerFilters { get; set; } = new() { "همه" };
    public List<string> CaseNumberFilters { get; set; } = new() { "همه" };
    public List<string> EngineerFilters { get; set; } = new() { "همه" };

    private int _totalCount;
    private int _filteredCount;
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
    public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
    public string CountDisplay => FilteredCount == TotalCount
        ? $"{TotalCount} مورد"
        : $"{FilteredCount} از {TotalCount} مورد";

    public bool HasActiveFilters => FilterDiscipline != "همه" || FilterOwner != "همه" ||
                                     FilterCaseNumber != "همه" || FilterEngineer != "همه" ||
                                     !string.IsNullOrWhiteSpace(SearchText);

    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand FilterByValueCommand { get; }

    private List<EngineerItem> _all = new();

    public EngineersViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(Load);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        FilterByValueCommand = new RelayCommand<string>(FilterByValue);
        Load();
    }

    private void Load()
    {
        Engineers.Clear();
        var snapshotId = _db.GetActiveSnapshotId();
        if (snapshotId == 0) { StatusMessage = "داده‌ای موجود نیست"; return; }
        var cases = _db.LoadCases(snapshotId);
        _all.Clear();
        foreach (var c in cases)
            foreach (var e in c.Engineers)
                _all.Add(new EngineerItem { CaseNumber = c.CaseNumber, Owner = c.Owner, Serial = c.Serial, Discipline = e.Discipline, Name = e.Name });

        // Build dynamic filter lists from actual data
        DisciplineFilters = FilterHelper.ExtractDistinctValues(_all, e => e.Discipline);
        OwnerFilters = FilterHelper.ExtractDistinctValues(_all, e => e.Owner);
        CaseNumberFilters = FilterHelper.ExtractDistinctValues(_all, e => e.CaseNumber);
        EngineerFilters = FilterHelper.ExtractDistinctValues(_all, e => e.Name);
        OnPropertyChanged(nameof(DisciplineFilters));
        OnPropertyChanged(nameof(OwnerFilters));
        OnPropertyChanged(nameof(CaseNumberFilters));
        OnPropertyChanged(nameof(EngineerFilters));

        TotalCount = _all.Count;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = FilterHelper.ApplyFilters(_all,
            (e => e.Discipline, FilterDiscipline),
            (e => e.Owner, FilterOwner),
            (e => e.CaseNumber, FilterCaseNumber),
            (e => e.Name, FilterEngineer));

        // General search on top of column filters
        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(e =>
                FilterHelper.Matches(e.Name, SearchText) ||
                FilterHelper.Matches(e.CaseNumber, SearchText) ||
                FilterHelper.Matches(e.Owner, SearchText) ||
                FilterHelper.Matches(e.Discipline, SearchText));

        Engineers.Clear();
        foreach (var item in filtered) Engineers.Add(item);
        FilteredCount = Engineers.Count;
        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(HasActiveFilters));
        StatusMessage = CountDisplay;
    }

    private void ClearFilters()
    {
        SearchText = "";
        FilterDiscipline = "همه";
        FilterOwner = "همه";
        FilterCaseNumber = "همه";
        FilterEngineer = "همه";
    }

    /// <summary>
    /// Filter by a specific cell value. The parameter format is "Column:Value".
    /// </summary>
    private void FilterByValue(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return;
        var parts = parameter.Split(':', 2);
        if (parts.Length != 2) return;
        var column = parts[0];
        var value = parts[1];

        switch (column)
        {
            case "Discipline": FilterDiscipline = value; break;
            case "Owner": FilterOwner = value; break;
            case "CaseNumber": FilterCaseNumber = value; break;
            case "Engineer": FilterEngineer = value; break;
        }
    }

    /// <summary>
    /// Remove filter for a specific column only.
    /// </summary>
    public void RemoveFilter(string column)
    {
        switch (column)
        {
            case "Discipline": FilterDiscipline = "همه"; break;
            case "Owner": FilterOwner = "همه"; break;
            case "CaseNumber": FilterCaseNumber = "همه"; break;
            case "Engineer": FilterEngineer = "همه"; break;
        }
    }
}

public sealed class EngineerItem
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string Name { get; set; } = "";
}
