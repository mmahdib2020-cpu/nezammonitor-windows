using System.Text.RegularExpressions;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Browser;

/// <summary>
/// Scrapes case data from the Nezam Engineering website using IBrowser.
/// Based on the legacy Python scraper (nezam_v2.py).
/// </summary>
public sealed class CaseScraper : ICaseScraper
{
    private readonly IWebBrowser _browser;
    private const string BaseUrl = "http://service.yazdnezam.ir:8033";
    private const string TableUrl = $"{BaseUrl}/panel/dashboard/khadamat/parvandeNezarat";

    public CaseScraper(IWebBrowser browser) => _browser = browser;

    public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        try
        {
            await _browser.NavigateAsync($"{BaseUrl}/panel/login", ct);
            await Task.Delay(5000, ct);

            await _browser.WaitForSelectorAsync("input[name=\"login\"]", TimeSpan.FromSeconds(15), ct);
            await _browser.FillAsync("input[name=\"login\"]", username, ct);
            await _browser.FillAsync("input[name=\"password\"]", password, ct);
            await _browser.ClickAsync("button:has-text('ورود')", ct);
            await Task.Delay(8000, ct);

            await CloseDialogsAsync(ct);
            return await IsLoggedInAsync(ct);
        }
        catch
        {
            return false;
        }
    }

    public async Task NavigateToTableAsync(CancellationToken ct = default)
    {
        await _browser.NavigateAsync(TableUrl, ct);
        await Task.Delay(10000, ct);
        await CloseDialogsAsync(ct);

        // Select Ardakan (checkbox index 1)
        var checks = await _browser.QuerySelectorAllAsync(".v-input--selection-controls__input", ct);
        if (checks.Count >= 2)
        {
            await checks[1].ClickAsync(ct);
            await Task.Delay(5000, ct);
        }

        // Click Monitoring tab
        await _browser.EvaluateAsync(
            "document.querySelectorAll('.v-tab').forEach(t => { if(t.textContent.trim().includes('نظارت')) t.click(); })", ct);
        await Task.Delay(12000, ct);
        await CloseDialogsAsync(ct);
    }

    public async Task<IReadOnlyList<Case>> ScrapeAllCasesAsync(
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        var cases = new List<Case>();
        var rows = await GetRowsAsync(ct);
        int total = rows.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            rows = await GetRowsAsync(ct);
            if (i >= rows.Count) break;

            var cells = await GetRowCellsAsync(rows[i], ct);
            if (cells.Count < 7) continue;

            statusCallback?.Invoke($"[{i + 1}/{total}] {cells[1]} | {cells[2]}");

            var basicCase = new Case
            {
                Serial = cells[0],
                CaseNumber = cells[1],
                Owner = cells[2],
                OwnerMobile = cells.Count > 3 ? cells[3] : "",
                Responsibility = cells.Count > 4 ? cells[4] : "",
                CapacityDate = cells.Count > 5 ? cells[5] : "",
                Office = cells.Count > 6 ? cells[6] : "",
            };

            cases.Add(basicCase);
        }

        return cases;
    }

    public async Task<Case> ScrapeCaseDetailsAsync(Case basicCase, CancellationToken ct = default)
    {
        var row = await FindRowAsync(basicCase.Serial, ct);
        if (row == null) return basicCase;

        var result = new Case
        {
            CaseNumber = basicCase.CaseNumber,
            Serial = basicCase.Serial,
            Owner = basicCase.Owner,
            OwnerMobile = basicCase.OwnerMobile,
            Responsibility = basicCase.Responsibility,
            CapacityDate = basicCase.CapacityDate,
            Office = basicCase.Office,
        };

        // Scrape engineers (column 10)
        try
        {
            if (await ClickCellButtonAsync(row, 10, ct))
            {
                await Task.Delay(5000, ct);
                var text = await _browser.EvaluateAsync("document.body.innerText", ct);
                result.Engineers = ParseEngineers(text);
                await CloseDialogsAsync(ct);
                await NavigateToTableIfNeededAsync(ct);
            }
        }
        catch { /* continue */ }

        // Scrape specifications — click last button to open dialog
        row = await FindRowAsync(basicCase.Serial, ct);
        if (row != null)
        {
            try
            {
                if (await ClickCellButtonAsync(row, 11, ct))
                {
                    await Task.Delay(5000, ct);
                    var text = await _browser.EvaluateAsync("document.body.innerText", ct);
                    result.Specification = ParseSpecifications(text);

                    // Extract usage type from dialog header: "4565 - مسكوني"
                    try
                    {
                        var titleText = await _browser.GetTextAsync(".v-card__title", ct);
                        if (!string.IsNullOrEmpty(titleText) && titleText.Contains("-"))
                        {
                            var parts = titleText.Split('-');
                            if (parts.Length == 2)
                            {
                                result.Specification ??= new CaseSpecification();
                                result.Specification.UsageType = parts[1].Trim();
                            }
                        }
                    }
                    catch { /* v-card__title not found */ }
                    await CloseDialogsAsync(ct);
                    await NavigateToTableIfNeededAsync(ct);
                }
            }
            catch { /* continue */ }
        }

        // Scrape fees (column 9)
        row = await FindRowAsync(basicCase.Serial, ct);
        if (row != null)
        {
            try
            {
                if (await ClickCellButtonAsync(row, 9, ct))
                {
                    await Task.Delay(5000, ct);
                    var text = await _browser.EvaluateAsync("document.body.innerText", ct);
                    result.Fees = ParseFees(text);
                    await NavigateToTableIfNeededAsync(ct);
                }
            }
            catch { /* continue */ }
        }

        // Scrape reports (column 8)
        row = await FindRowAsync(basicCase.Serial, ct);
        if (row != null)
        {
            try
            {
                var tds = await row.QuerySelectorAllAsync("td");
                if (tds.Count > 8)
                {
                    var btn = await tds[8].QuerySelectorAsync("button");
                    if (btn != null)
                    {
                        await btn.ClickAsync(ct);
                        await Task.Delay(8000, ct);
                        result.Reports = await ParseReportsAsync(ct);
                        await NavigateToTableAsync(ct);
                    }
                }
            }
            catch { /* report extract error ignored */ }
        }

        return result;
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        return await _browser.EvaluateAsync(
            "(!window.location.href.includes('login')).toString()", ct) == "true";
    }

    public async Task CloseDialogsAsync(CancellationToken ct = default)
    {
        await _browser.CloseDialogsAsync(ct);
    }

    // ========================= Private helpers =========================


    /// <summary>
    /// Scrape details with selective options (what to extract).
    /// </summary>
    public async Task<Case> ScrapeCaseDetailsWithOptionsAsync(
        Case basicCase, ExtractionOptions options, CancellationToken ct = default)
    {
        var row = await FindRowAsync(basicCase.Serial, ct);
        if (row == null) return basicCase;

        var result = new Case
        {
            CaseNumber = basicCase.CaseNumber,
            Serial = basicCase.Serial,
            Owner = basicCase.Owner,
            OwnerMobile = basicCase.OwnerMobile,
            Responsibility = basicCase.Responsibility,
            CapacityDate = basicCase.CapacityDate,
            Office = basicCase.Office,
        };

        // Engineers
        if (options.ExtractEngineers)
        {
            try
            {
                if (await ClickCellButtonAsync(row, 10, ct))
                {
                    await Task.Delay(5000, ct);
                    var text = await _browser.EvaluateAsync("document.body.innerText", ct);
                    result.Engineers = ParseEngineers(text);
                    await CloseDialogsAsync(ct);
                    await NavigateToTableIfNeededAsync(ct);
                }
            }
            catch { }
        }

        // Specifications
        if (options.ExtractSpecifications)
        {
            row = await FindRowAsync(basicCase.Serial, ct);
            if (row != null)
            {
                try
                {
                    if (await ClickCellButtonAsync(row, 11, ct))
                    {
                        await Task.Delay(5000, ct);
                        var text = await _browser.EvaluateAsync("document.body.innerText", ct);
                        result.Specification = ParseSpecifications(text);
                        await CloseDialogsAsync(ct);
                        await NavigateToTableIfNeededAsync(ct);
                    }
                }
                catch { }
            }
        }

        // Fees
        if (options.ExtractFees)
        {
            row = await FindRowAsync(basicCase.Serial, ct);
            if (row != null)
            {
                try
                {
                    if (await ClickCellButtonAsync(row, 9, ct))
                    {
                        await Task.Delay(5000, ct);
                        var text = await _browser.EvaluateAsync("document.body.innerText", ct);
                        result.Fees = ParseFees(text);
                        await NavigateToTableIfNeededAsync(ct);
                    }
                }
                catch { }
            }
        }

        // Reports
        if (options.ExtractReports)
        {
            row = await FindRowAsync(basicCase.Serial, ct);
            if (row != null)
            {
                try
                {
                    var tds = await row.QuerySelectorAllAsync("td");
                    if (tds.Count > 8)
                    {
                        var btn = await tds[8].QuerySelectorAsync("button");
                        if (btn != null)
                        {
                            await btn.ClickAsync(ct);
                            await Task.Delay(8000, ct);
                            result.Reports = await ParseReportsAsync(ct);
                            await NavigateToTableAsync(ct);
                        }
                    }
                }
                catch { /* report error ignored */ }
            }
        }

        return result;
    }

    /// <summary>
    /// Full extraction with options: start-from, selective data, pause/resume.
    /// Returns (cases extracted so far, last index).
    /// </summary>
    public async Task<(List<Case> Cases, int LastIndex)> ExtractWithOptionsAsync(
        ExtractionOptions options,
        Action<string>? statusCallback = null,
        Action<int, int>? progressCallback = null,
        Action<List<Case>>? incrementalSaveCallback = null,
        NezamMonitor.Core.Data.NezamDatabase? db = null)
    {
        var cases = new List<Case>();
        var ct = options.CancellationTokenSource?.Token ?? CancellationToken.None;
        var rows = await GetRowsAsync(ct);
        int total = rows.Count;

        int startIdx = Math.Max(0, Math.Min(options.StartFromIndex - 1, total));
        int endIdx = options.MaxCases > 0
            ? Math.Min(startIdx + options.MaxCases, total)
            : total;


        for (int i = startIdx; i < endIdx; i++)
        {
            ct.ThrowIfCancellationRequested();

            // Wait if paused
            options.PauseEvent?.Wait(ct);

            // Re-query rows since navigation invalidates references
            rows = await GetRowsAsync(ct);
            if (i >= rows.Count) break;

            var cells = await GetRowCellsAsync(rows[i], ct);
            if (cells.Count < 7) continue;

            var serial = cells[0];
            var caseNumber = cells[1];
            var owner = cells[2];

            statusCallback?.Invoke($"[{i + 1}/{total}] {serial} | {caseNumber} | {owner}");
            progressCallback?.Invoke(i + 1, total);

            var basicCase = new Case
            {
                Serial = serial,
                CaseNumber = caseNumber,
                Owner = owner,
                OwnerMobile = cells.Count > 3 ? cells[3] : "",
                Responsibility = cells.Count > 4 ? cells[4] : "",
                CapacityDate = cells.Count > 5 ? cells[5] : "",
                Office = cells.Count > 6 ? cells[6] : "",
            };

            // Filter: skip complete cases if requested
            if (options.FilterIncompleteOnly)
            {
                var specOk = !options.ExtractSpecifications || db?.LoadSpecificationForCase(basicCase.CaseNumber) != null;
                var reportsOk = !options.ExtractReports || (db?.LoadReportCountForCase(basicCase.CaseNumber) ?? 0) > 0;
                var engineersOk = !options.ExtractEngineers || (db?.LoadEngineerCountForCase(basicCase.CaseNumber) ?? 0) > 0;
                var feesOk = !options.ExtractFees || (db?.LoadFeeCountForCase(basicCase.CaseNumber) ?? 0) > 0;
                bool isComplete = specOk && reportsOk && engineersOk && feesOk;
                if (isComplete)
                {
                    statusCallback?.Invoke($"  SKIP: {owner} (all selected data exists)");
                    continue;
                }
            }

            try
            {
                var detailed = await ScrapeCaseDetailsWithOptionsAsync(basicCase, options, ct);

                // For specs: skip if UpdateAllSpecifications is false and spec is already complete
                if (!options.UpdateAllSpecifications && detailed.Specification != null)
                {
                    // Check if spec is already complete (has key fields filled)
                    var s = detailed.Specification;
                    bool isComplete = !string.IsNullOrEmpty(s.BuildingGroup)
                        && !string.IsNullOrEmpty(s.PermitNumber)
                        && !string.IsNullOrEmpty(s.StructureType);
                    if (isComplete)
                    {
                        detailed.Specification = null; // Already complete, don't overwrite
                    }
                }

                cases.Add(detailed);

                // Incremental save
                incrementalSaveCallback?.Invoke(cases);
                statusCallback?.Invoke($"  OK: {owner} ({cases.Count} extracted)");
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"  FAIL: {owner} - {ex.Message}");
            }

            // Delay between cases
            if (options.DelayBetweenCasesMs > 0 && i < endIdx - 1)
                await Task.Delay(options.DelayBetweenCasesMs, ct);
        }

        return (cases, endIdx >= total ? total - 1 : endIdx - 1);
    }

    private async Task<List<IBrowserElement>> GetRowsAsync(CancellationToken ct)
    {
        var rows = new List<IBrowserElement>();
        try
        {
            var allRows = await _browser.QuerySelectorAllAsync("table tbody tr", ct);
            foreach (var row in allRows)
            {
                var tds = await row.QuerySelectorAllAsync("td");
                if (tds.Count >= 7)
                {
                    var num = (await tds[0].GetTextAsync()).Trim();
                    if (num.Length > 0 && (char.IsDigit(num[0]) || "۰۱۲۳۴۵۶۷۸۹".Contains(num[0])))
                    {
                        rows.Add(row);
                    }
                }
            }
        }
        catch { }
        return rows;
    }

    private async Task<List<string>> GetRowCellsAsync(IBrowserElement row, CancellationToken ct)
    {
        var cells = new List<string>();
        try
        {
            var tds = await row.QuerySelectorAllAsync("td");
            foreach (var td in tds)
            {
                cells.Add((await td.GetTextAsync()).Trim());
            }
        }
        catch { }
        return cells;
    }

    private async Task<IBrowserElement?> FindRowAsync(string serial, CancellationToken ct)
    {
        var rows = await GetRowsAsync(ct);
        foreach (var row in rows)
        {
            var tds = await row.QuerySelectorAllAsync("td");
            if (tds.Count > 0)
            {
                var num = (await tds[0].GetTextAsync()).Trim();
                if (num == serial) return row;
            }
        }
        return null;
    }

    private async Task<bool> ClickCellButtonAsync(IBrowserElement row, int colIndex, CancellationToken ct)
    {
        try
        {
            var tds = await row.QuerySelectorAllAsync("td");
            if (tds.Count > colIndex)
            {
                var btn = await tds[colIndex].QuerySelectorAsync("button");
                if (btn != null)
                {
                    await btn.ClickAsync(ct);
                    await Task.Delay(5000, ct);
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Click the last button in a table row (for specs dialog).
    /// </summary>
    private async Task<bool> ClickLastRowButtonAsync(IBrowserElement row, CancellationToken ct)
    {
        try
        {
            var buttons = await row.QuerySelectorAllAsync("button");
            if (buttons.Count >= 1)
            {
                await buttons[^1].ClickAsync(ct);
                await Task.Delay(5000, ct);
                return true;
            }
        }
        catch { }
        return false;
    }

    private async Task NavigateToTableIfNeededAsync(CancellationToken ct)
    {
        var url = _browser.Url;
        if (!url.Contains("parvandeNezarat") || url.Contains("/mali/"))
        {
            await NavigateToTableAsync(ct);
        }
    }

    private async Task<List<ReportRecord>> ParseReportsAsync(CancellationToken ct)
    {
        var reports = new List<ReportRecord>();
        try
        {
            var tables = await _browser.QuerySelectorAllAsync("table", ct);
            foreach (var table in tables)
            {
                var firstRow = await table.QuerySelectorAsync("tr");
                if (firstRow != null)
                {
                    var headerText = await firstRow.GetTextAsync();
                    if (headerText.Contains("نوع گزارش") || headerText.Contains("مرحله"))
                    {
                        var rows = await table.QuerySelectorAllAsync("tr");
                        foreach (var r in rows)
                        {
                            var tds = await r.QuerySelectorAllAsync("td");
                            if (tds.Count < 9) continue;
                            var num = (await tds[0].GetTextAsync()).Trim();
                            if (string.IsNullOrEmpty(num)) continue;
                            if (!char.IsDigit(num[0]) && !"۰۱۲۳۴۵۶۷۸۹".Contains(num[0])) continue;

                            bool hasFile = false;
                            try
                            {
                                var btn = await tds[8].QuerySelectorAsync("button");
                                hasFile = btn != null;
                            }
                            catch { }

                            reports.Add(new ReportRecord(
                                num,
                                tds.Count > 1 ? (await tds[1].GetTextAsync()).Trim() : "",
                                tds.Count > 2 ? (await tds[2].GetTextAsync()).Trim() : "",
                                tds.Count > 3 ? (await tds[3].GetTextAsync()).Trim() : "",
                                tds.Count > 4 ? (await tds[4].GetTextAsync()).Trim() : "",
                                tds.Count > 5 ? (await tds[5].GetTextAsync()).Trim() : "",
                                tds.Count > 6 ? (await tds[6].GetTextAsync()).Trim() : "",
                                tds.Count > 7 ? (await tds[7].GetTextAsync()).Trim() : "",
                                hasFile));
                        }
                        break;
                    }
                }
            }
        }
        catch { }
        return reports;
    }

    // ========================= Text parsers =========================

    public static List<Engineer> ParseEngineers(string text)
    {
        var result = new List<Engineer>();
        var patterns = new (string discipline, string pattern)[]
        {
            ("معماری", @"معماري\s*:\s*\n?\s*(.+)"),
            ("عمران", @"عمران\s*:\s*\n?\s*(.+)"),
            ("مکانیک", @"مكانيك\s*:\s*\n?\s*(.+)"),
            ("برق", @"برق\s*:\s*\n?\s*(.+)"),
            ("هماهنگ‌کننده", @"هماهنگ\s*كننده\s*:\s*\n?\s*(.+)"),
        };

        foreach (var (discipline, pattern) in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                var value = match.Groups[1].Value.Trim().Split('\n')[0].Trim();
                if (!value.Contains(':'))
                {
                    result.Add(new Engineer(discipline, value));
                }
            }
        }
        return result;
    }

    public static CaseSpecification? ParseSpecifications(string text)
    {
        var specs = new CaseSpecification();
        var patterns = new (string field, string pattern)[]
        {
            (nameof(CaseSpecification.BuildingGroup), @"گروه ساختمان[یي]\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.RenovationCode), @"کد نوساز[یي]\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.PlanInstructionNo), @"شماره دستور تهیه نقشه\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.PlanInstructionType), @"نوع دستور تهیه نقشه\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.LandArea), @"مساحت زمین\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.ParafArea), @"متراژ پاراف\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.PlanInstructionDate), @"تاریخ دستور تهیه نقشه\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.StructureType), @"نوع سازه\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.BlockTitle), @"عنوان بلوک\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.BlockCount), @"تعداد بلوک\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.Floors), @"تعداد طبقات\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.Units), @"تعداد واحد\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.Issuer), @"صادر کننده\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.PermitNumber), @"شماره پروانه\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.PermitDate), @"تاریخ صدور پروانه\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.ReleaseDate), @"تاریخ ترخیص\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.PlanZone), @"محدوده طرح\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.Address), @"آدرس\s*:\s*\n\s*(.+)"),
            (nameof(CaseSpecification.CapacityArea), @"متراژ کسر ظرفیت\s*[^:]*:\s*\n?\s*([\d.,]+)"),
            (nameof(CaseSpecification.UsageType), @"نوع کاربری\s*:\s*\n\s*(.+)"),
        };

        bool found = false;
        foreach (var (field, pattern) in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                var value = match.Groups[1].Value.Trim().Split('\n')[0].Trim();
                typeof(CaseSpecification).GetProperty(field)?.SetValue(specs, value);
                found = true;
            }
        }

        return found ? specs : null;
    }

    public static List<Fee> ParseFees(string text)
    {
        var fees = new List<Fee>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("ریال") && trimmed.Contains('\t'))
            {
                var parts = trimmed.Split('\t');
                if (parts.Length >= 7)
                {
                    fees.Add(new Fee(
                        parts.Length > 0 ? parts[0] : "",
                        parts.Length > 1 ? parts[1] : "",
                        parts.Length > 6 ? parts[6] : "",
                        parts.Length > 3 ? parts[3] : "",
                        parts.Length > 4 ? parts[4] : "",
                        parts.Length > 5 ? parts[5] : "",
                        parts.Length > 7 ? parts[7] : "",
                        parts.Length > 8 ? parts[8] : "",
                        parts.Length > 9 ? parts[9] : ""));
                }
            }
        }
        return fees;
    }
}
