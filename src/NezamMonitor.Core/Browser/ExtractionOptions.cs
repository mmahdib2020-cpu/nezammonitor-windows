namespace NezamMonitor.Core.Browser;

/// <summary>
/// Options for controlling what data to extract and how.
/// Supports selective extraction and pause/resume.
/// </summary>
public sealed class ExtractionOptions
{
    /// <summary>Start extraction from this case index (0-based). 0 = from beginning.</summary>
    public int StartFromIndex { get; set; } = 0;

    /// <summary>Maximum cases to extract. 0 = unlimited (all remaining).</summary>
    public int MaxCases { get; set; } = 0;

    /// <summary>Extract engineer/supervisor data (ناظرین).</summary>
    public bool ExtractEngineers { get; set; } = true;

    /// <summary>Extract fee data (حق‌الزحمه).</summary>
    public bool ExtractFees { get; set; } = true;

    /// <summary>Extract specification/block detail data (توضیحات).</summary>
    public bool ExtractSpecifications { get; set; } = true;

    /// <summary>
    /// Specification extraction mode:
    /// false = only update cases with incomplete/empty specs
    /// true = update all cases regardless
    /// </summary>
    public bool UpdateAllSpecifications { get; set; } = false;

    /// <summary>Extract report data (گزارش‌ها).</summary>
    public bool ExtractReports { get; set; } = true;

    /// <summary>Run headless (no visible browser).</summary>
    public bool Headless { get; set; } = false;

    /// <summary>Delay between cases in milliseconds (for rate limiting).</summary>
    public int DelayBetweenCasesMs { get; set; } = 1000;

    /// <summary>CancellationToken source for pause/stop.</summary>
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public System.Threading.ManualResetEventSlim? PauseEvent { get; set; }
    public bool FilterIncompleteOnly { get; set; }
}

/// <summary>
/// Result of a full or partial extraction run.
/// Supports incremental saving.
/// </summary>
public sealed class ExtractionResult
{
    public int TotalDiscovered { get; set; }
    public int Extracted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int LastIndex { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<string> Errors { get; set; } = new();

    public TimeSpan Duration => EndTime - StartTime;
}
