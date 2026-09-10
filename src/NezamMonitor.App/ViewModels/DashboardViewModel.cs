using NezamMonitor.Core.Data;

namespace NezamMonitor.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private int _totalCases;
    private int _totalEngineers;
    private int _totalFees;
    private int _totalReports;
    private int _totalSnapshots;
    private string _lastUpdate = "هنوز انجام نشده";
    private string _connectionStatus = "غير متصل";

    public int TotalCases { get => _totalCases; set => SetProperty(ref _totalCases, value); }
    public int TotalEngineers { get => _totalEngineers; set => SetProperty(ref _totalEngineers, value); }
    public int TotalFees { get => _totalFees; set => SetProperty(ref _totalFees, value); }
    public int TotalReports { get => _totalReports; set => SetProperty(ref _totalReports, value); }
    public int TotalSnapshots { get => _totalSnapshots; set => SetProperty(ref _totalSnapshots, value); }
    public string LastUpdate { get => _lastUpdate; set => SetProperty(ref _lastUpdate, value); }
    public string ConnectionStatus { get => _connectionStatus; set => SetProperty(ref _connectionStatus, value); }

    public DashboardViewModel(NezamDatabase db)
    {
        _db = db;
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId > 0)
            {
                var cases = _db.LoadCases(snapshotId);
                TotalCases = cases.Count;
                TotalEngineers = cases.Sum(c => c.Engineers.Count);
                TotalFees = cases.Sum(c => c.Fees.Count);
                TotalReports = cases.Sum(c => c.Reports.Count);
                ConnectionStatus = "متصل - اسنپ‌شات " + snapshotId;
                LastUpdate = cases.Count + " پرونده فعال";
            }
            else
            {
                ConnectionStatus = "غیر متصل";
                LastUpdate = "هنوز داده‌ای موجود نیست";
            }
        }
        catch
        {
            ConnectionStatus = "خطا در بارگذاری";
        }
    }
}
