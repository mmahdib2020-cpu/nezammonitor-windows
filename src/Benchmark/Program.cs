using System.Diagnostics;
using NezamMonitor.Core.Browser;
using NezamMonitor.Core.Models;

// ============================================================
// EXTRACTION BENCHMARK — Phase 0 Baseline
// ============================================================
// This tool measures extraction timing for the CURRENT implementation.
// It requires a running Playwright browser and valid credentials.
// ============================================================

var baseUrl = "http://service.yazdnezam.ir:8033";
var username = Environment.GetEnvironmentVariable("NEZAM_USER") ?? "";
var password = Environment.GetEnvironmentVariable("NEZAM_PASS") ?? "";

if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
{
    Console.WriteLine("ERROR: Set NEZAM_USER and NEZAM_PASS environment variables");
    Console.WriteLine("Usage: NEZAM_USER=xxx NEZAM_PASS=yyy dotnet run");
    return;
}

int maxCases = int.TryParse(Environment.GetEnvironmentVariable("MAX_CASES"), out var m) ? m : 1;

Console.WriteLine("=== EXTRACTION BENCHMARK ===");
Console.WriteLine($"Max cases: {maxCases}");
Console.WriteLine($"Base URL: {baseUrl}");
Console.WriteLine();

var sw = Stopwatch.StartNew();

// Launch browser
Console.WriteLine("[1] Launching browser...");
var browser = new PlaywrightBrowser();
await browser.LaunchAsync(headless: false);

var scraper = new CaseScraper(browser);

// Login
Console.WriteLine("[2] Logging in...");
var loginSw = Stopwatch.StartNew();
var loginOk = await scraper.LoginAsync(username, password);
loginSw.Stop();
Console.WriteLine($"    Login: {(loginOk ? "OK" : "FAILED")} ({loginSw.ElapsedMilliseconds}ms)");
if (!loginOk) { await browser.DisposeAsync(); return; }

// Navigate to table
Console.WriteLine("[3] Navigating to table...");
var navSw = Stopwatch.StartNew();
await scraper.NavigateToTableAsync();
navSw.Stop();
Console.WriteLine($"    Navigation: {navSw.ElapsedMilliseconds}ms");

// Get initial row count
var initRows = await browser.QuerySelectorAllAsync("table tbody tr");
Console.WriteLine($"    Table rows: {initRows.Count}");

// Extract cases
Console.WriteLine($"[4] Extracting {maxCases} case(s)...");
var options = new ExtractionOptions
{
    MaxCases = maxCases,
    StartFromIndex = 0,
    ExtractEngineers = true,
    ExtractFees = true,
    ExtractSpecifications = true,
    ExtractReports = true,
    UpdateAllSpecifications = true,
    FilterIncompleteOnly = false,
    DelayBetweenCasesMs = 0,
};

var extractionSw = Stopwatch.StartNew();
var (cases, _) = await scraper.ExtractWithOptionsAsync(
    options,
    msg => Console.WriteLine($"    {msg}"),
    (cur, total) => { },
    null);
extractionSw.Stop();

sw.Stop();

// Results
Console.WriteLine();
Console.WriteLine("=== BASELINE RESULTS ===");
Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalSeconds:F1}s)");
Console.WriteLine($"Login time: {loginSw.ElapsedMilliseconds}ms");
Console.WriteLine($"Navigation time: {navSw.ElapsedMilliseconds}ms");
Console.WriteLine($"Extraction time: {extractionSw.ElapsedMilliseconds}ms ({extractionSw.TotalSeconds:F1}s)");
Console.WriteLine();
Console.WriteLine($"Cases extracted: {cases.Count}");

var totalEngineers = cases.Sum(c => c.Engineers.Count);
var totalFees = cases.Sum(c => c.Fees.Count);
var totalReports = cases.Sum(c => c.Reports.Count);
var casesWithSpecs = cases.Count(c => c.Specification != null);
var casesWithUsageType = cases.Count(c => c.Specification?.UsageType == "مسكوني" || c.Specification?.UsageType == "مسکونی");

Console.WriteLine($"Engineers total: {totalEngineers}");
Console.WriteLine($"Fees total: {totalFees}");
Console.WriteLine($"Reports total: {totalReports}");
Console.WriteLine($"Cases with specs: {casesWithSpecs}/{cases.Count}");
Console.WriteLine($"Cases with UsageType: {casesWithUsageType}/{cases.Count}");

// Per-case breakdown
if (cases.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("=== PER-CASE DETAILS ===");
    foreach (var c in cases)
    {
        Console.WriteLine($"  {c.CaseNumber} | {c.Owner} | Engineers:{c.Engineers.Count} Fees:{c.Fees.Count} Reports:{c.Reports.Count} Spec:{(c.Specification != null ? "Y" : "N")} UsageType:{c.Specification?.UsageType ?? "EMPTY"}");
    }
}

if (cases.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"Average per case: {extractionSw.ElapsedMilliseconds / cases.Count}ms");
}

await browser.DisposeAsync();
Console.WriteLine();
Console.WriteLine("BENCHMARK COMPLETE");
