using System.Text.RegularExpressions;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Browser;

/// <summary>
/// Scrapes case data from the Nezam Engineering website using IBrowser.
/// Uses targeted DOM extraction instead of body.innerText + Regex where possible.
/// </summary>
public sealed class CaseScraper : ICaseScraper
{
    private readonly IWebBrowser _browser;
    private const string BaseUrl = "http://service.yazdnezam.ir:8033";
    private const string TableUrl = $"{BaseUrl}/panel/dashboard/khadamat/parvandeNezarat";

    // Dialog wait timeout — maximum time to wait for a dialog to appear
    private static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(10);
    // Table wait timeout — maximum time to wait for table rows (SPA can be slow)
    private static readonly TimeSpan TableTimeout = TimeSpan.FromSeconds(30);
    // Small fallback delay when no specific condition is available
    private const int FallbackDelayMs = 500;

    public CaseScraper(IWebBrowser browser) => _browser = browser;

    public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        try
        {
            await _browser.NavigateAsync($"{BaseUrl}/panel/login", ct);
            await _browser.WaitForSelectorAsync("input[name=\"login\"]", TimeSpan.FromSeconds(15), ct);

            await _browser.FillAsync("input[name=\"login\"]", username, ct);
            await _browser.FillAsync("input[name=\"password\"]", password, ct);
            await _browser.ClickAsync("button:has-text('ورود')", ct);

            // Wait for navigation away from login page
            try
            {
                await _browser.WaitForSelectorAsync("input[name=\"login\"]", TimeSpan.FromSeconds(3), ct);
                // If we can still see login input, login may have failed — but wait a bit more
            }
            catch { /* Expected: login input disappears after successful login */ }

            await Task.Delay(FallbackDelayMs, ct);
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

        // Wait for page to load
        await _browser.WaitForSelectorAsync("table", TableTimeout, ct);

        // Step 1: Close persistent "اطلاعات بیشتر" dialog
        // This dialog blocks all interaction — must be dismissed first
        try
        {
            var closeBtns = await _browser.QuerySelectorAllAsync(".v-dialog--active button", ct);
            foreach (var btn in closeBtns)
            {
                var btnText = await btn.GetTextAsync();
                if (btnText.Contains("بستن"))
                {
                    await btn.ClickAsync(ct);
                    await Task.Delay(1000, ct);
                    break;
                }
            }
        }
        catch { }

        // Also try clicking any visible "بستن" button in active dialogs
        await CloseDialogsAsync(ct);
        await Task.Delay(500, ct);

        // Step 2: Select "اردكان" — it's a radio button, not a checkbox
        // Try multiple selectors for the radio/selection control
        try
        {
            var ardakanClicked = false;

            // Method A: Click by label text via JavaScript
            var clicked = await _browser.EvaluateAsync("""
                (() => {
                    // Find all labels/text in selection controls
                    const labels = document.querySelectorAll('.v-input--radio-group .v-radio label, .v-input--selection-controls label');
                    for (const l of labels) {
                        if (l.textContent.trim().includes('اردكان') || l.textContent.trim().includes('اردکان')) {
                            l.click();
                            return 'clicked_label';
                        }
                    }
                    // Try clicking the radio input directly
                    const radios = document.querySelectorAll('.v-radio');
                    for (const r of radios) {
                        if (r.textContent.includes('اردكان') || r.textContent.includes('اردکان')) {
                            r.querySelector('input')?.click() || r.click();
                            return 'clicked_radio';
                        }
                    }
                    // Try selection controls
                    const controls = document.querySelectorAll('.v-input--selection-controls__input');
                    if (controls.length >= 2) {
                        controls[1].click();
                        return 'clicked_control_1';
                    }
                    return 'not_found';
                })()
            """, ct);
            Console.WriteLine($"  [DEBUG] Ardakan selection: {clicked}");
            ardakanClicked = clicked != "not_found";

            if (!ardakanClicked)
            {
                // Fallback: try clicking the second selection control
                var checks = await _browser.QuerySelectorAllAsync(".v-input--selection-controls__input", ct);
                if (checks.Count >= 2)
                {
                    await checks[1].ClickAsync(ct);
                    ardakanClicked = true;
                    Console.WriteLine("  [DEBUG] Ardakan: clicked second selection control (fallback)");
                }
            }

            if (ardakanClicked)
            {
                await Task.Delay(2000, ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [DEBUG] Ardakan selection error: {ex.Message}");
        }

        // Step 3: Click "نظارت" tab
        try
        {
            await _browser.EvaluateAsync("""
                (() => {
                    const tabs = document.querySelectorAll('.v-tab');
                    for (const t of tabs) {
                        if (t.textContent.trim().includes('نظارت')) {
                            t.click();
                            return 'clicked';
                        }
                    }
                    return 'not_found';
                })()
            """, ct);
            Console.WriteLine("  [DEBUG] Monitoring tab: clicked");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [DEBUG] Tab click error: {ex.Message}");
        }

        // Step 4: Wait for table data to load
        await Task.Delay(3000, ct);
        await _browser.WaitForSelectorAsync("table tbody tr", TableTimeout, ct);
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

        // Engineers (column 10)
        try
        {
            if (await ClickCellButtonAsync(row, 10, ct))
            {
                await WaitForDialogAsync(ct);
                result.Engineers = await ExtractEngineersFromDialogAsync(ct);
                await CloseDialogsAsync(ct);
                await NavigateToTableIfNeededAsync(ct);
            }
        }
        catch { /* continue */ }

        // Specifications (column 11)
        row = await FindRowAsync(basicCase.Serial, ct);
        if (row != null)
        {
            try
            {
                if (await ClickCellButtonAsync(row, 11, ct))
                {
                    await WaitForDialogAsync(ct);
                    result.Specification = await ExtractSpecificationsFromDialogAsync(ct);
                    await CloseDialogsAsync(ct);
                    await NavigateToTableIfNeededAsync(ct);
                }
            }
            catch { /* continue */ }
        }

        // Fees (column 9)
        row = await FindRowAsync(basicCase.Serial, ct);
        if (row != null)
        {
            try
            {
                if (await ClickCellButtonAsync(row, 9, ct))
                {
                    await WaitForDialogAsync(ct);
                    result.Fees = await ExtractFeesFromDialogAsync(ct);
                    await CloseDialogsAsync(ct);
                    await NavigateToTableIfNeededAsync(ct);
                }
            }
            catch { /* continue */ }
        }

        // Reports (column 8) — already DOM-based, keep existing pattern with better waits
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
                        await _browser.WaitForSelectorAsync("table", TableTimeout, ct);
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

        // Engineers — TRUE DOM extraction
        if (options.ExtractEngineers)
        {
            try
            {
                if (await ClickCellButtonAsync(row, 10, ct))
                {
                    await WaitForDialogAsync(ct);
                    result.Engineers = await ExtractEngineersFromDialogAsync(ct);
                    await CloseDialogsAsync(ct);
                    if (_browser.Url.Contains("/mali/"))
                        await NavigateToTableAsync(ct);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Engineers failed for {basicCase.CaseNumber}: {ex.Message}");
            }
        }

        // Specifications — TRUE DOM extraction
        if (options.ExtractSpecifications)
        {
            row = await FindRowAsync(basicCase.Serial, ct);
            if (row != null)
            {
                try
                {
                    if (await ClickCellButtonAsync(row, 11, ct))
                    {
                        await WaitForDialogAsync(ct);
                        result.Specification = await ExtractSpecificationsFromDialogAsync(ct);
                        await CloseDialogsAsync(ct);
                        if (_browser.Url.Contains("/mali/"))
                            await NavigateToTableAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Specs failed for {basicCase.CaseNumber}: {ex.Message}");
                }
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
                        await WaitForDialogAsync(ct);
                        result.Fees = await ExtractFeesFromDialogAsync(ct);
                        // CRITICAL: DO NOT call CloseDialogsAsync here.
                        // The "بستن" button in fees dialog navigates to /mali/ page.
                        // Just navigate back to table directly.
                        await NavigateToTableAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Fees failed for {basicCase.CaseNumber}: {ex.Message}");
                }
            }
        }

        // Reports — navigates to /mali/ page, needs fresh row lookup after return
        if (options.ExtractReports)
        {
            row = await FindRowAsync(basicCase.Serial, ct);
            if (row == null)
            {
                System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Reports: row not found for serial={basicCase.Serial}, URL={_browser.Url}");
                Console.WriteLine($"  [REPORTS] row NOT found for {basicCase.Serial}, URL={_browser.Url}");
            }
            if (row != null)
            {
                try
                {
                    var tds = await row.QuerySelectorAllAsync("td");
                    System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Reports: row found, tds={tds.Count}");
                    if (tds.Count > 8)
                    {
                        var btn = await tds[8].QuerySelectorAsync("button");
                        System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Reports: button={btn != null}");
                        if (btn != null)
                        {
                            await btn.ClickAsync(ct);
                            await _browser.WaitForSelectorAsync("table", TableTimeout, ct);
                            result.Reports = await ParseReportsAsync(ct);
                            System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Reports: extracted {result.Reports.Count} reports");
                            await NavigateToTableAsync(ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EXTRACTION] Reports failed for {basicCase.CaseNumber}: {ex.Message}");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Full extraction with options: start-from, selective data, pause/resume.
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
            options.PauseEvent?.Wait(ct);

            // Re-query rows after navigation/dialog close
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

                if (!options.UpdateAllSpecifications && detailed.Specification != null)
                {
                    var s = detailed.Specification;
                    bool isComplete = !string.IsNullOrEmpty(s.BuildingGroup)
                        && !string.IsNullOrEmpty(s.PermitNumber)
                        && !string.IsNullOrEmpty(s.StructureType);
                    if (isComplete)
                    {
                        detailed.Specification = null;
                    }
                }

                cases.Add(detailed);
                incrementalSaveCallback?.Invoke(cases);
                statusCallback?.Invoke($"  OK: {owner} ({cases.Count} extracted)");
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"  FAIL: {owner} - {ex.Message}");
            }

            if (options.DelayBetweenCasesMs > 0 && i < endIdx - 1)
                await Task.Delay(options.DelayBetweenCasesMs, ct);
        }

        return (cases, endIdx >= total ? total - 1 : endIdx - 1);
    }

    // ========================= DOM Extraction Helpers =========================

    /// <summary>
    /// Wait for a Vuetify dialog to appear and be visible.
    /// </summary>
    private async Task WaitForDialogAsync(CancellationToken ct)
    {
        try
        {
            await _browser.WaitForSelectorAsync(".v-dialog--active", DialogTimeout, ct);
        }
        catch
        {
            // Fallback: try alternate dialog selectors
            try
            {
                await _browser.WaitForSelectorAsync(".v-dialog__content--active", DialogTimeout, ct);
            }
            catch
            {
                // Last resort: small delay
                await Task.Delay(FallbackDelayMs, ct);
            }
        }
    }

    /// <summary>
    /// Extract engineers from the active dialog using TRUE DOM extraction.
    /// Queries: .v-dialog--active .row → &lt;b&gt; (label) + .mr-1 (value)
    /// Falls back to body.innerText + Regex if DOM extraction fails.
    /// </summary>
    private async Task<List<Engineer>> ExtractEngineersFromDialogAsync(CancellationToken ct)
    {
        try
        {
            // TRUE DOM EXTRACTION: query individual label/value elements
            var result = await _browser.EvaluateAsync("""
                (() => {
                    const dialog = document.querySelector('.v-dialog--active');
                    if (!dialog) return JSON.stringify([]);
                    const rows = dialog.querySelectorAll('.row');
                    const engineers = [];
                    for (const row of rows) {
                        const b = row.querySelector('b');
                        const val = row.querySelector('.mr-1');
                        if (b && val) {
                            const label = b.textContent.trim().replace(':', '').replace(' :', '').trim();
                            const value = val.textContent.trim();
                            if (label && value && !label.includes(':')) {
                                engineers.push({label: label, value: value});
                            }
                        }
                    }
                    return JSON.stringify(engineers);
                })()
            """, ct);

            var pairs = System.Text.Json.JsonSerializer.Deserialize<List<LabelValue>>(result);
            if (pairs != null && pairs.Count > 0)
            {
                var engineers = new List<Engineer>();
                var disciplineMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["معماري"] = "معماری", ["عمران"] = "عمران", ["مكانيك"] = "مکانیک",
                    ["برق"] = "برق", ["هماهنگ كننده"] = "هماهنگ‌کننده",
                };
                foreach (var p in pairs)
                {
                    var normalizedLabel = p.Label.Trim();
                    if (disciplineMap.TryGetValue(normalizedLabel, out var engDiscipline))
                    {
                        engineers.Add(new Engineer(engDiscipline, p.Value));
                    }
                }
                if (engineers.Count > 0)
                    return engineers;
            }
        }
        catch { }

        // Fallback: body.innerText + Regex (legacy)
        try
        {
            var text = await _browser.EvaluateAsync("document.body.innerText", ct);
            return ParseEngineers(text);
        }
        catch { return new List<Engineer>(); }
    }

    /// <summary>
    /// Extract specifications from the active dialog using TRUE DOM extraction.
    /// UsageType from .v-card__title, other fields from .row → &lt;b&gt; + .mr-1.
    /// Falls back to body.innerText + Regex if DOM extraction fails.
    /// </summary>
    private async Task<CaseSpecification?> ExtractSpecificationsFromDialogAsync(CancellationToken ct)
    {
        var specs = new CaseSpecification();
        bool found = false;

        try
        {
            // UsageType from .v-card__title: "4565 - مسكوني"
            try
            {
                var titleText = await _browser.GetTextAsync(".v-card__title", ct);
                if (!string.IsNullOrEmpty(titleText) && titleText.Contains("-"))
                {
                    var parts = titleText.Split('-');
                    if (parts.Length == 2)
                    {
                        specs.UsageType = parts[1].Trim();
                        found = true;
                    }
                }
            }
            catch { }

            // TRUE DOM EXTRACTION: query .row → <b> (label) + .mr-1 (value)
            var result = await _browser.EvaluateAsync("""
                (() => {
                    const dialog = document.querySelector('.v-dialog--active');
                    if (!dialog) return JSON.stringify([]);
                    const rows = dialog.querySelectorAll('.row');
                    const pairs = [];
                    for (const row of rows) {
                        const b = row.querySelector('b');
                        const val = row.querySelector('.mr-1');
                        if (b && val) {
                            const label = b.textContent.trim().replace(':', '').replace(' :', '').trim();
                            const value = val.textContent.trim();
                            if (label && value && !label.includes(':')) {
                                pairs.push({label: label, value: value});
                            }
                        }
                    }
                    return JSON.stringify(pairs);
                })()
            """, ct);

            var pairs = System.Text.Json.JsonSerializer.Deserialize<List<LabelValue>>(result);
            if (pairs != null && pairs.Count > 0)
            {
                // Map Persian labels → C# property names
                var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["گروه ساختمانی"] = nameof(CaseSpecification.BuildingGroup),
                    ["گروه ساختماني"] = nameof(CaseSpecification.BuildingGroup),
                    ["کد نوسازی"] = nameof(CaseSpecification.RenovationCode),
                    ["کد نوسازي"] = nameof(CaseSpecification.RenovationCode),
                    ["شماره دستور تهیه نقشه"] = nameof(CaseSpecification.PlanInstructionNo),
                    ["نوع دستور تهیه نقشه"] = nameof(CaseSpecification.PlanInstructionType),
                    ["مساحت زمین"] = nameof(CaseSpecification.LandArea),
                    ["مساحت زمين"] = nameof(CaseSpecification.LandArea),
                    ["متراژ پاراف"] = nameof(CaseSpecification.ParafArea),
                    ["تاریخ دستور تهیه نقشه"] = nameof(CaseSpecification.PlanInstructionDate),
                    ["نوع سازه"] = nameof(CaseSpecification.StructureType),
                    ["عنوان بلوک"] = nameof(CaseSpecification.BlockTitle),
                    ["عنوان بلوک"] = nameof(CaseSpecification.BlockTitle),
                    ["تعداد بلوک"] = nameof(CaseSpecification.BlockCount),
                    ["تعداد طبقات"] = nameof(CaseSpecification.Floors),
                    ["تعداد واحد"] = nameof(CaseSpecification.Units),
                    ["صادر کننده"] = nameof(CaseSpecification.Issuer),
                    ["شماره پروانه"] = nameof(CaseSpecification.PermitNumber),
                    ["تاریخ صدور پروانه"] = nameof(CaseSpecification.PermitDate),
                    ["تاریخ ترخیص"] = nameof(CaseSpecification.ReleaseDate),
                    ["محدوده طرح"] = nameof(CaseSpecification.PlanZone),
                    ["آدرس"] = nameof(CaseSpecification.Address),
                    ["متراژ کسر ظرفیت نظارت"] = nameof(CaseSpecification.CapacityArea),
                    ["متراژ کسر ظرفیت"] = nameof(CaseSpecification.CapacityArea),
                };

                foreach (var p in pairs)
                {
                    var normalizedLabel = p.Label.Trim();
                    if (fieldMap.TryGetValue(normalizedLabel, out var propertyName))
                    {
                        // For CapacityArea, extract numeric part
                        var value = p.Value;
                        if (propertyName == nameof(CaseSpecification.CapacityArea))
                        {
                            var numMatch = System.Text.RegularExpressions.Regex.Match(value, @"[\d.,]+");
                            if (numMatch.Success) value = numMatch.Value;
                        }
                        typeof(CaseSpecification).GetProperty(propertyName)?.SetValue(specs, value);
                        found = true;
                    }
                }
            }
        }
        catch
        {
            try
            {
                var text = await _browser.EvaluateAsync("document.body.innerText", ct);
                return ParseSpecifications(text);
            }
            catch { return null; }
        }

        return found ? specs : null;
    }

    /// <summary>
    /// Extract fees from the active dialog using DOM table extraction.
    /// </summary>
    private async Task<List<Fee>> ExtractFeesFromDialogAsync(CancellationToken ct)
    {
        try
        {
            var fees = new List<Fee>();

            // Try to find a table inside the dialog
            var dialogTables = await _browser.QuerySelectorAllAsync(".v-dialog--active table", ct);
            if (dialogTables.Count == 0)
            {
                try
                {
                    dialogTables = await _browser.QuerySelectorAllAsync(".v-dialog__content--active table", ct);
                }
                catch { }
            }

            if (dialogTables.Count > 0)
            {
                var table = dialogTables[0];
                var rows = await table.QuerySelectorAllAsync("tr");
                bool headerPassed = false;

                foreach (var row in rows)
                {
                    var tds = await row.QuerySelectorAllAsync("td");
                    if (tds.Count < 7) 
                    {
                        // Check if this is a header row
                        var headerText = await row.GetTextAsync();
                        if (headerText.Contains("رشته") || headerText.Contains("مرحله"))
                            headerPassed = true;
                        continue;
                    }

                    var cells = new List<string>();
                    foreach (var td in tds)
                    {
                        cells.Add((await td.GetTextAsync()).Trim());
                    }

                    // Fee row structure: discipline, serviceType, [skip], stage, startDate, endDate, amount, payStatus, confirmStatus, amountType, description
                    fees.Add(new Fee(
                        cells.Count > 0 ? cells[0] : "",    // discipline
                        cells.Count > 1 ? cells[1] : "",    // serviceType
                        cells.Count > 6 ? cells[6] : "",    // stage
                        cells.Count > 3 ? cells[3] : "",    // startDate
                        cells.Count > 4 ? cells[4] : "",    // endDate
                        cells.Count > 5 ? cells[5] : "",    // amount
                        cells.Count > 7 ? cells[7] : "",    // payStatus
                        cells.Count > 8 ? cells[8] : "",    // confirmStatus
                        cells.Count > 9 ? cells[9] : ""     // amountType
                    ));
                }

                if (fees.Count > 0)
                    return fees;
            }
        }
        catch { }

        // Fallback: body.innerText parsing
        try
        {
            var text = await _browser.EvaluateAsync("document.body.innerText", ct);
            return ParseFees(text);
        }
        catch { return new List<Fee>(); }
    }

    // ========================= Table / Row Helpers =========================

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

    // ===== Public wrappers for reports fallback =====

    /// <summary>Find a table row by serial for reports extraction.</summary>
    public async Task<IBrowserElement?> FindRowForReportsAsync(string serial, CancellationToken ct = default)
        => await FindRowAsync(serial, ct);

    /// <summary>Click reports button on a row and extract report records.</summary>
    public async Task<List<ReportRecord>> ExtractReportsFromRowAsync(IBrowserElement row, CancellationToken ct = default)
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
                    await _browser.WaitForSelectorAsync("table", TableTimeout, ct);
                    var reports = await ParseReportsAsync(ct);
                    await NavigateToTableAsync(ct);
                    return reports;
                }
            }
        }
        catch { }
        return new();
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
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private async Task NavigateToTableIfNeededAsync(CancellationToken ct)
    {
        var url = _browser.Url;
        // Only re-navigate if we're on a completely different page (e.g. reports /mali/ page)
        // Don't re-navigate just because URL changed slightly within the table section
        if (url.Contains("/mali/"))
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

    // ========================= Legacy Text Parsers (fallback only) =========================

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
            (nameof(CaseSpecification.BuildingGroup), @"گروه ساختمان[یي]\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.RenovationCode), @"کد نوساز[یي]\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.PlanInstructionNo), @"شماره دستور تهیه نقشه\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.PlanInstructionType), @"نوع دستور تهیه نقشه\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.LandArea), @"مساحت زمین\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.ParafArea), @"متراژ پاراف\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.PlanInstructionDate), @"تاریخ دستور تهیه نقشه\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.StructureType), @"نوع سازه\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.BlockTitle), @"عنوان بلوک\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.BlockCount), @"تعداد بلوک\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.Floors), @"تعداد طبقات\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.Units), @"تعداد واحد\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.Issuer), @"صادر کننده\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.PermitNumber), @"شماره پروانه\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.PermitDate), @"تاریخ صدور پروانه\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.ReleaseDate), @"تاریخ ترخیص\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.PlanZone), @"محدوده طرح\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.Address), @"آدرس\s*:\s*\n?\s*(.+)"),
            (nameof(CaseSpecification.CapacityArea), @"متراژ کسر ظرفیت\s*[^:]*:\s*\n?\s*([\d.,]+)"),
            (nameof(CaseSpecification.UsageType), @"نوع کاربری\s*:\s*\n?\s*(.+)"),
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

/// <summary>
/// Helper for JSON deserialization of DOM label-value pairs.
/// </summary>
internal sealed class LabelValue
{
    [System.Text.Json.Serialization.JsonPropertyName("label")]
    public string Label { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public string Value { get; set; } = "";
}
