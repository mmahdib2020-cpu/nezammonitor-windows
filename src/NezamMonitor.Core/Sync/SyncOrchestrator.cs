using NezamMonitor.Core.Browser;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Diff;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Sync;

/// <summary>
/// Orchestrates synchronization: scrape → stage → validate → snapshot → diff → persist.
/// </summary>
public class SyncOrchestrator
{
    private readonly NezamDatabase _db;
    private readonly ICaseScraper _scraper;

    public event Action<string>? StatusChanged;
    public event Action<double>? ProgressChanged;

    public SyncOrchestrator(NezamDatabase db, ICaseScraper scraper)
    {
        _db = db;
        _scraper = scraper;
    }

    /// <summary>
    /// Run a full synchronization cycle.
    /// </summary>
    public async Task<SyncResult> RunSyncAsync(CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new SyncResult { StartTime = startTime };

        try
        {
            // 1. Navigate to table
            StatusChanged?.Invoke("ناوبری به جدول...");
            ProgressChanged?.Invoke(10);
            await _scraper.NavigateToTableAsync(ct);

            // 2. Scrape basic cases
            StatusChanged?.Invoke("استخراج پرونده‌ها...");
            ProgressChanged?.Invoke(20);
            var cases = (await _scraper.ScrapeAllCasesAsync(
                msg => StatusChanged?.Invoke(msg), ct)).ToList();
            result.DiscoveredCases = cases.Count;

            // 3. Scrape details for each case
            for (int i = 0; i < cases.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                StatusChanged?.Invoke($"جزئیات [{i + 1}/{cases.Count}] {cases[i].Owner}");
                ProgressChanged?.Invoke(20 + 60.0 * i / cases.Count);

                try
                {
                    cases[i] = await _scraper.ScrapeCaseDetailsAsync(cases[i], ct);
                    result.ScrapedDetails++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"خطا در {cases[i].CaseNumber}: {ex.Message}");
                }
            }

            // 4. Create snapshot
            StatusChanged?.Invoke("ذخیره اسنپ‌شات...");
            ProgressChanged?.Invoke(85);

            var snapshotId = _db.CreateSnapshot(DateTime.UtcNow);

            // Compare with previous snapshot
            var prevSnapshotId = _db.GetLastValidSnapshotId();
            if (prevSnapshotId > 0)
            {
                var prevCases = _db.LoadCases(prevSnapshotId);
                var diff = SnapshotComparer.Compare(prevCases, cases);

                result.NewCases = diff.Added.Count;
                result.RemovedCases = diff.Removed.Count;
                result.ModifiedCases = diff.Modified.Count;
                result.UnchangedCases = diff.Unchanged.Count;

                // Persist changes
                if (diff.FieldChanges.Count > 0)
                {
                    _db.SaveChanges(snapshotId, diff.FieldChanges);
                }
            }
            else
            {
                result.NewCases = cases.Count;
            }

            // Save snapshot data
            _db.SaveSnapshot(snapshotId, cases);
            _db.FinalizeSnapshot(snapshotId, cases.Count);
            result.SnapshotId = snapshotId;

            StatusChanged?.Invoke("تکمیل همگام‌سازی");
            ProgressChanged?.Invoke(100);
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            StatusChanged?.Invoke("لغو شد");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"خطای کلی: {ex.Message}");
            StatusChanged?.Invoke($"خطا: {ex.Message}");
        }

        result.FinishTime = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Load the latest snapshot.
    /// </summary>
    public List<Case> LoadLatestSnapshot()
    {
        var id = _db.GetLastValidSnapshotId();
        return id > 0 ? _db.LoadCases(id) : new List<Case>();
    }
}

/// <summary>
/// Result of a synchronization operation.
/// </summary>
public sealed class SyncResult
{
    public DateTime StartTime { get; set; }
    public DateTime FinishTime { get; set; }
    public TimeSpan Duration => FinishTime - StartTime;
    public long SnapshotId { get; set; }
    public int DiscoveredCases { get; set; }
    public int ScrapedDetails { get; set; }
    public int NewCases { get; set; }
    public int RemovedCases { get; set; }
    public int ModifiedCases { get; set; }
    public int UnchangedCases { get; set; }
    public bool Cancelled { get; set; }
    public List<string> Errors { get; } = new();
}
