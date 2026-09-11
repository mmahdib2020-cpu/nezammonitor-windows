using NezamMonitor.App.ViewModels;
using System.Threading;
using System.Windows.Input;
using NezamMonitor.Core.Browser;
using NezamMonitor.Core.Data;

namespace NezamMonitor.App.Services;

/// <summary>
/// Singleton extraction controller shared between MainWindow and UpdateView.
/// Provides commands for Start, Stop, Pause/Resume, LoadLatest.
/// </summary>
public sealed class ExtractionController : ViewModelBase
{
    private static ExtractionController? _instance;
    private static readonly object _lock = new();

    private bool _isBusy;
    private bool _isPaused;
    private string _statusMessage = "آماده";
    private double _progress;
    private string _logText = "";
    private CancellationTokenSource? _cts;
    private ManualResetEventSlim? _pauseEvent;

    public static ExtractionController Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                    _instance ??= new ExtractionController();
            return _instance;
        }
    }

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public bool IsPaused { get => _isPaused; set => SetProperty(ref _isPaused, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }
    private int _maxCases;
    public int MaxCases { get => _maxCases; set => SetProperty(ref _maxCases, value); }
    private bool _filterIncompleteOnly;
    public bool FilterIncompleteOnly { get => _filterIncompleteOnly; set => SetProperty(ref _filterIncompleteOnly, value); }
    private bool _extractEngineers = true;
    public bool ExtractEngineers { get => _extractEngineers; set => SetProperty(ref _extractEngineers, value); }
    private bool _extractFees = true;
    public bool ExtractFees { get => _extractFees; set => SetProperty(ref _extractFees, value); }
    private bool _extractSpecifications = true;
    public bool ExtractSpecifications { get => _extractSpecifications; set => SetProperty(ref _extractSpecifications, value); }
    private bool _extractReports = true;
    public bool ExtractReports { get => _extractReports; set => SetProperty(ref _extractReports, value); }
    private bool _updateAllSpecifications = true;
    public bool UpdateAllSpecifications { get => _updateAllSpecifications; set => SetProperty(ref _updateAllSpecifications, value); }
    private int _startFromIndex = 1;
    public int StartFromIndex { get => _startFromIndex; set => SetProperty(ref _startFromIndex, value); }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand LoadLatestCommand { get; }

    private ExtractionController()
    {
        StartCommand = new AsyncRelayCommand(StartExtractionAsync, () => !IsBusy);
        StopCommand = new RelayCommand(StopExtraction, () => IsBusy);
        PauseResumeCommand = new RelayCommand(PauseResume);
        LoadLatestCommand = new RelayCommand(LoadLatest);
    }

    public void AppendLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    private async Task StartExtractionAsync()
    {
        IsBusy = true;
        IsPaused = false;
        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);

        try
        {
            var db = DatabaseService.Instance;
            var username = "";
            var password = "";
            var settings = db.LoadSettings();
            if (settings.TryGetValue("username", out var u)) username = u;
            if (settings.TryGetValue("password", out var p)) password = p;

            AppendLog("شروع استخراج...");
            StatusMessage = "در حال راه‌اندازی مرورگر...";

            PlaywrightBrowser? browser = null;
            try
            {
                browser = new PlaywrightBrowser();
                await browser.LaunchAsync(headless: false);
                AppendLog("مرورگر راه‌اندازی شد ✓");

                var scraper = new CaseScraper(browser);
                StatusMessage = "در حال ورود...";
                var loginOk = await scraper.LoginAsync(username, password);
                if (!loginOk)
                {
                    AppendLog("خطا: ورود ناموفق");
                    StatusMessage = "ورود ناموفق";
                    return;
                }

                AppendLog("ورود موفق ✓");
                await scraper.NavigateToTableAsync();
                AppendLog("ناوبری به جدول موفق ✓");

                var options = new ExtractionOptions
                {
                    StartFromIndex = StartFromIndex,
                    MaxCases = MaxCases,
                    ExtractEngineers = ExtractEngineers,
                    ExtractFees = ExtractFees,
                    ExtractSpecifications = ExtractSpecifications,
                    UpdateAllSpecifications = UpdateAllSpecifications,
                    ExtractReports = ExtractReports,
                    CancellationTokenSource = _cts,
                    PauseEvent = _pauseEvent,
                    FilterIncompleteOnly = FilterIncompleteOnly,
                };

                StatusMessage = "در حال استخراج...";
                var startTime = DateTime.Now;

                var snapshotId = db.CreateSnapshot(startTime);
                var (cases, lastIndex) = await scraper.ExtractWithOptionsAsync(
                    options,
                    msg => AppendLog(msg),
                    (current, total) =>
                    {
                        Progress = (double)current / total * 100;
                        StatusMessage = IsPaused
                            ? $"متوقف شده — {current}/{total}"
                            : $"استخراج {current}/{total}";
                    },
                    incrementalCases =>
                    {
                        // Log progress only - all data saved at the end by FinalizeSnapshot
                        try
                        {
                            if (incrementalCases.Count > 0)
                            {
                                var last = incrementalCases[incrementalCases.Count - 1];
                                AppendLog($"استخراج شد: {last.CaseNumber} — {last.Owner}");
                            }
                        }
                        catch { }
                    },
                    db);

                var elapsed = DateTime.Now - startTime;
                AppendLog($"تکمیل: {cases.Count} پرونده در {elapsed.TotalSeconds:F0} ثانیه");

                if (cases.Count > 0)
                {
                    db.SaveSnapshot(snapshotId, cases);
                    db.FinalizeSnapshot(snapshotId, cases.Count);
                    db.SetActiveSnapshot(snapshotId);
                    AppendLog($"ذخیره شد (snapshot #{snapshotId}) ✓ — {cases.Count} پرونده فعال شد");
                }
                else
                {
                    AppendLog("پرونده‌ای استخراج نشد");
                }

                StatusMessage = $"تکمیل — {cases.Count} پرونده";
            }
            finally
            {
                browser?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("استخراج متوقف شد.");
            StatusMessage = "متوقف شد";
        }
        catch (Exception ex)
        {
            AppendLog($"خطا: {ex.Message}");
            StatusMessage = "خطا";
        }
        finally
        {
            IsBusy = false;
            IsPaused = false;
            Progress = 0;
            _cts?.Dispose();
            _cts = null;
            _pauseEvent?.Dispose();
            _pauseEvent = null;
        }
    }

    private void StopExtraction()
    {
        _cts?.Cancel();
        _pauseEvent?.Set();
        StatusMessage = "در حال توقف...";
    }

    private void PauseResume()
    {
        if (_pauseEvent == null) return;

        if (IsPaused)
        {
            _pauseEvent.Set();
            IsPaused = false;
            AppendLog("▶ ادامه استخراج");
            StatusMessage = "در حال استخراج...";
        }
        else
        {
            _pauseEvent.Reset();
            IsPaused = true;
            AppendLog("⏸ وقفه");
            StatusMessage = "متوقف شده...";
        }
    }

    private void LoadLatest()
    {
        try
        {
            var db = DatabaseService.Instance;
            var snapshotId = db.GetLastValidSnapshotId();
            if (snapshotId == 0)
            {
                AppendLog("هنوز داده‌ای ذخیره نشده");
                StatusMessage = "داده‌ای موجود نیست";
                return;
            }
            var cases = db.LoadCases(snapshotId);
            AppendLog($"آخرین اسنپ‌شات: {cases.Count} پرونده بارگذاری شد");
            StatusMessage = $"{cases.Count} پرونده بارگذاری شد";
        }
        catch (Exception ex)
        {
            AppendLog($"خطا: {ex.Message}");
            StatusMessage = "خطا";
        }
    }
}
