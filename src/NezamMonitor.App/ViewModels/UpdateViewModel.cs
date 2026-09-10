using System.ComponentModel;
using System.Threading;
using System.Windows.Input;
using NezamMonitor.Core.Browser;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Sync;

namespace NezamMonitor.App.ViewModels;

public sealed class UpdateViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _statusMessage = "آماده همگام‌سازی";
    private double _progress;
    private bool _isBusy;
    private string _username = "";
    private string _password = "";
    private string _logText = "";
    private SyncResult? _lastResult;
    private bool _isConnected;
    private bool _rememberMe;

    // Extraction options
    private int _startFromIndex = 1;
    private bool _extractEngineers = true;
    private bool _extractFees = true;
    private bool _extractSpecifications = true;
    private bool _updateAllSpecifications;
    private bool _extractReports = true;

    private CancellationTokenSource? _cts;
    private ManualResetEventSlim? _pauseEvent;
    private bool _isPaused;

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string Username { get => _username; set {
        SetProperty(ref _username, value);
        if (!string.IsNullOrEmpty(value))
            _db.SaveSetting("username", value);
    } }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }
    public SyncResult? LastResult { get => _lastResult; set => SetProperty(ref _lastResult, value); }
    public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }
    public bool RememberMe { get => _rememberMe; set => SetProperty(ref _rememberMe, value); }
    public bool IsPaused { get => _isPaused; set => SetProperty(ref _isPaused, value); }

    public int StartFromIndex { get => _startFromIndex; set => SetProperty(ref _startFromIndex, value); }
    public bool ExtractEngineers { get => _extractEngineers; set => SetProperty(ref _extractEngineers, value); }
    public bool ExtractFees { get => _extractFees; set => SetProperty(ref _extractFees, value); }
    public bool ExtractSpecifications { get => _extractSpecifications; set => SetProperty(ref _extractSpecifications, value); }
    public bool UpdateAllSpecifications { get => _updateAllSpecifications; set => SetProperty(ref _updateAllSpecifications, value); }
    public bool ExtractReports { get => _extractReports; set => SetProperty(ref _extractReports, value); }

    public ICommand SyncCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand LoadLatestCommand { get; }

    public UpdateViewModel(NezamDatabase db)
    {
        _db = db;
        SyncCommand = new AsyncRelayCommand(ExecuteSyncAsync, () => !IsBusy);
        StopCommand = new RelayCommand(StopSync, () => IsBusy);
        PauseResumeCommand = new RelayCommand(PauseResume, () => IsBusy);
        LoadLatestCommand = new RelayCommand(LoadLatest, () => !IsBusy);

        var settings = _db.LoadSettings();
        if (settings.TryGetValue("username", out var u)) Username = u;
        if (settings.TryGetValue("password", out var p)) Password = p;
    }

    private async Task ExecuteSyncAsync()
    {
        IsBusy = true;
        IsPaused = false;
        LogText = "";
        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true); // Start unpaused

        try
        {
            if (RememberMe)
            {
                _db.SaveSetting("username", Username);
                // Never save password - security
                _db.SaveSetting("password", "");
            }

            AppendLog("شروع همگام‌سازی...");
            StatusMessage = "در حال راه‌اندازی مرورگر...";

            PlaywrightBrowser? browser = null;
            try
            {
                browser = new PlaywrightBrowser();
                await browser.LaunchAsync(headless: false);
                AppendLog("مرورگر راه‌اندازی شد ✓");

                var scraper = new CaseScraper(browser);
                StatusMessage = "در حال ورود...";
                var loginOk = await scraper.LoginAsync(Username, Password);
                if (!loginOk)
                {
                    AppendLog("خطا: نام کاربری یا رمز عبور اشتباه است");
                    StatusMessage = "ورود ناموفق";
                    return;
                }

                IsConnected = true;
                AppendLog("ورود موفق ✓");
                await scraper.NavigateToTableAsync();
                AppendLog("ناوبری به جدول موفق ✓");

                AppendLog("تعداد کل پرونده‌ها: ۷۱ (طبق جدول سایت)");

                // Configure extraction options
                var options = new ExtractionOptions
                {
                    StartFromIndex = StartFromIndex,
                    ExtractEngineers = ExtractEngineers,
                    ExtractFees = ExtractFees,
                    ExtractSpecifications = ExtractSpecifications,
                    UpdateAllSpecifications = UpdateAllSpecifications,
                    ExtractReports = ExtractReports,
                    CancellationTokenSource = _cts,
                    PauseEvent = _pauseEvent,
                };

                AppendLog($"شروع از پرونده: {StartFromIndex + 1}");
                AppendLog($"ناظرین: {(ExtractEngineers ? "✓" : "✗")}");
                AppendLog($"حق‌الزحمه: {(ExtractFees ? "✓" : "✗")}");
                AppendLog($"مشخصات: {(ExtractSpecifications ? "✓" : "✗")}");
                if (ExtractSpecifications)
                    AppendLog($"  حالت: {(UpdateAllSpecifications ? "تمام مشخصات" : "فقط ناقص")}");
                AppendLog($"گزارش‌ها: {(ExtractReports ? "✓" : "✗")}");
                AppendLog("─".PadRight(40));

                // Run extraction
                StatusMessage = "در حال استخراج...";
                var startTime = DateTime.Now;

                var (cases, lastIndex) = await scraper.ExtractWithOptionsAsync(
                    options,
                    msg => AppendLog(msg),
                    (current, total) =>
                    {
                        Progress = (double)current / total * 100;
                        StatusMessage = IsPaused
                            ? $"متوقف شده — {current}/{total}"
                            : $"استخراج {current}/{total}";
                    });

                var elapsed = DateTime.Now - startTime;
                AppendLog("");
                AppendLog("═══ نتیجه استخراج ═══");
                AppendLog($"تعداد: {cases.Count} پرونده");
                AppendLog($"مدت: {elapsed.TotalSeconds:F1} ثانیه");

                // Save to database
                if (cases.Count > 0)
                {
                    AppendLog("ذخیره در دیتابیس...");
                    var snapshotId = _db.CreateSnapshot(startTime);
                    _db.SaveSnapshot(snapshotId, cases);
                    _db.FinalizeSnapshot(snapshotId, cases.Count);
                    AppendLog($"ذخیره شد (snapshot #{snapshotId}) ✓");
                }

                // Generate Excel
                if (cases.Count > 0)
                {
                    try
                    {
                        var exportDir = System.IO.Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory, "exports");
                        System.IO.Directory.CreateDirectory(exportDir);
                        var excelPath = System.IO.Path.Combine(exportDir,
                            $"Cases_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                        NezamMonitor.Core.Excel.ExcelExporter.GenerateFullWorkbook(cases, excelPath);
                        AppendLog("فایل Excel ایجاد شد:");
                        AppendLog(excelPath);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"خطا در ایجاد Excel: {ex.Message}");
                    }
                }

                StatusMessage = $"تکمیل — {cases.Count} پرونده در {elapsed.TotalSeconds:F0} ثانیه";
            }
            finally
            {
                browser?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("استخراج توسط کاربر متوقف شد.");
            StatusMessage = "متوقف شد";
        }
        catch (InvalidOperationException ex)
        {
            AppendLog($"خطا در راه‌اندازی مرورگر: {ex.Message}");
            StatusMessage = "خطا در راه‌اندازی مرورگر";
        }
        catch (Exception ex)
        {
            AppendLog($"خطای پیش‌بینی نشده: {ex.GetType().Name}: {ex.Message}");
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

    private void PauseResume()
    {
        if (_pauseEvent == null) return;

        if (IsPaused)
        {
            // Resume
            _pauseEvent.Set();
            IsPaused = false;
            AppendLog("▶ ادامه استخراج");
            StatusMessage = "در حال استخراج...";
        }
        else
        {
            // Pause
            _pauseEvent.Reset();
            IsPaused = true;
            AppendLog("⏸ وقفه در استخراج");
            StatusMessage = "متوقف شده...";
        }
    }

    private void StopSync()
    {
        _cts?.Cancel();
        _pauseEvent?.Set(); // Unblock if paused so cancellation can proceed
        StatusMessage = "در حال توقف...";
    }

    private void LoadLatest()
    {
        try
        {
            var snapshotId = _db.GetLastValidSnapshotId();
            if (snapshotId == 0)
            {
                AppendLog("هنوز داده‌ای ذخیره نشده است");
                StatusMessage = "داده‌ای موجود نیست";
                return;
            }
            var cases = _db.LoadCases(snapshotId);
            AppendLog($"آخرین اسنپ‌شات: {cases.Count} پرونده بارگذاری شد");
            StatusMessage = $"{cases.Count} پرونده بارگذاری شد";
        }
        catch (Exception ex)
        {
            AppendLog($"خطا در بارگذاری: {ex.Message}");
            StatusMessage = "خطا";
        }
    }

    private void AppendLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }
}
