using Microsoft.Playwright;
using System.Collections.Concurrent;

if (args.Length < 2) { Console.WriteLine("Usage: dotnet run -- <user> <pass>"); return; }

var pw = await Playwright.CreateAsync();
string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
if (!File.Exists(edgePath)) edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";

var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
    ExecutablePath = edgePath,
    Args = new[] { "--no-sandbox" }
});
var ctx = await browser.NewContextAsync(new BrowserNewContextOptions
{
    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
});
var page = await ctx.NewPageAsync();
page.SetDefaultTimeout(30000);

var navOpts = new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded };

// Track network requests per step
var netLog = new List<string>();

page.Request += (_, req) =>
{
    var url = req.Url;
    var method = req.Method;
    var rt = req.ResourceType;
    if (rt == "xhr" || rt == "fetch")
    {
        var pd = "";
        try { pd = req.PostData ?? ""; } catch { }
        var line = $"  {DateTime.Now:HH:mm:ss.fff} {method} [{rt}] {url}";
        if (!string.IsNullOrEmpty(pd)) line += $"\n    POST: {pd.Substring(0, Math.Min(pd.Length, 300))}";
        netLog.Add(line);
    }
};

page.Response += async (_, resp) =>
{
    var url = resp.Url;
    var rt = resp.Request.ResourceType;
    if ((rt == "xhr" || rt == "fetch") && (url.Contains("api") || url.Contains("parvande") || url.Contains("nezarat") ||
        url.Contains("mali") || url.Contains("get") || url.Contains("load") || url.Contains("data") || url.Contains("json")))
    {
        try
        {
            var body = System.Text.Encoding.UTF8.GetString(await resp.BodyAsync());
            var preview = body.Length > 500 ? body.Substring(0, 500) : body;
            netLog.Add($"  [RESP {resp.Status}] {url}\n    Body: {preview}");
        }
        catch { }
    }
};

async Task DumpNet(string step)
{
    Console.WriteLine($"\n=== {step} — Network Requests ({netLog.Count}) ===");
    foreach (var l in netLog) Console.WriteLine(l);
    netLog.Clear();
}

Console.WriteLine("=== NETWORK DISCOVERY ===");

// LOGIN
Console.WriteLine("\n=== LOGIN ===");
await page.GotoAsync("http://service.yazdnezam.ir:8033/panel/login", navOpts);
await page.WaitForSelectorAsync("input[name=\"login\"]");
await page.FillAsync("input[name=\"login\"]", args[0]);
await page.FillAsync("input[name=\"password\"]", args[1]);
await page.ClickAsync("button:has-text('ورود')");
await Task.Delay(5000);
Console.WriteLine($"URL: {page.Url}");
await DumpNet("POST-LOGIN");

// NAVIGATE TO TABLE
Console.WriteLine("\n=== NAVIGATE TO TABLE ===");
await page.GotoAsync("http://service.yazdnezam.ir:8033/panel/dashboard/khadamat/parvandeNezarat", navOpts);
await Task.Delay(8000);
Console.WriteLine($"URL: {page.Url}");
await DumpNet("POST-NAVIGATE");

// Close persistent dialog
try
{
    var btns = await page.QuerySelectorAllAsync(".v-dialog--active button");
    foreach (var b in btns) { var t = await b.InnerTextAsync(); if (t.Trim() == "بستن") { await b.ClickAsync(); await Task.Delay(2000); break; } }
} catch { }
await Task.Delay(1000);
await DumpNet("AFTER-DIALOG-CLOSE");

// Select Ardakan
try
{
    await page.EvaluateAsync("(() => { const l = document.querySelectorAll('.v-input--radio-group .v-radio label, .v-input--selection-controls label'); for (const x of l) { if (x.textContent.trim().includes('اردكان')) { x.click(); return; } } })()");
    await Task.Delay(3000);
} catch { }
await DumpNet("AFTER-ARDAKAN");

// Click Monitoring tab
try
{
    await page.EvaluateAsync("(() => { const t = document.querySelectorAll('.v-tab'); for (const x of t) { if (x.textContent.trim().includes('نظارت')) { x.click(); return; } } })()");
    await Task.Delay(5000);
} catch { }
await DumpNet("AFTER-TAB");

var tableRows = await page.QuerySelectorAllAsync("table tbody tr");
Console.WriteLine($"\nTable rows: {tableRows.Count}");

// Get first row serial
string serial = "";
if (tableRows.Count > 0)
{
    var tds = await tableRows[0].QuerySelectorAllAsync("td");
    if (tds.Count > 0) serial = (await tds[0].InnerTextAsync()).Trim();
}
Console.WriteLine($"First serial: {serial}");

// ENGINEERS
Console.WriteLine("\n=== ENGINEERS BUTTON ===");
netLog.Clear();
try
{
    tableRows = await page.QuerySelectorAllAsync("table tbody tr");
    var tds = await tableRows[0].QuerySelectorAllAsync("td");
    if (tds.Count > 10)
    {
        var btn = await tds[10].QuerySelectorAsync("button");
        if (btn != null) { await btn.ClickAsync(); await Task.Delay(5000); }
    }
} catch { }
Console.WriteLine($"URL: {page.Url}");
await DumpNet("ENGINEERS");

// Close + navigate back
try { var cb = await page.QuerySelectorAllAsync(".v-dialog--active button"); foreach (var b in cb) { var t = await b.InnerTextAsync(); if (t.Trim() == "بستن") { await b.ClickAsync(); break; } } } catch { }
await Task.Delay(2000);
if (page.Url.Contains("/mali/")) await page.GotoAsync("http://service.yazdnezam.ir:8033/panel/dashboard/khadamat/parvandeNezarat", navOpts);
await Task.Delay(5000);

// SPECS
Console.WriteLine("\n=== SPECS BUTTON ===");
netLog.Clear();
try
{
    tableRows = await page.QuerySelectorAllAsync("table tbody tr");
    var tds = await tableRows[0].QuerySelectorAllAsync("td");
    if (tds.Count > 11)
    {
        var btn = await tds[11].QuerySelectorAsync("button");
        if (btn != null) { await btn.ClickAsync(); await Task.Delay(5000); }
    }
} catch { }
Console.WriteLine($"URL: {page.Url}");
await DumpNet("SPECS");

// Close + navigate back
try { var cb = await page.QuerySelectorAllAsync(".v-dialog--active button"); foreach (var b in cb) { var t = await b.InnerTextAsync(); if (t.Trim() == "بستن") { await b.ClickAsync(); break; } } } catch { }
await Task.Delay(2000);
if (page.Url.Contains("/mali/")) await page.GotoAsync("http://service.yazdnezam.ir:8033/panel/dashboard/khadamat/parvandeNezarat", navOpts);
await Task.Delay(5000);

// FEES
Console.WriteLine("\n=== FEES BUTTON ===");
netLog.Clear();
try
{
    tableRows = await page.QuerySelectorAllAsync("table tbody tr");
    var tds = await tableRows[0].QuerySelectorAllAsync("td");
    if (tds.Count > 9)
    {
        var btn = await tds[9].QuerySelectorAsync("button");
        if (btn != null) { await btn.ClickAsync(); await Task.Delay(5000); }
    }
} catch { }
Console.WriteLine($"URL: {page.Url}");
await DumpNet("FEES");

// Close + navigate back
try { var cb = await page.QuerySelectorAllAsync(".v-dialog--active button"); foreach (var b in cb) { var t = await b.InnerTextAsync(); if (t.Trim() == "بستن") { await b.ClickAsync(); break; } } } catch { }
await Task.Delay(2000);

// REPORTS
Console.WriteLine("\n=== REPORTS BUTTON ===");
netLog.Clear();
try
{
    tableRows = await page.QuerySelectorAllAsync("table tbody tr");
    var tds = await tableRows[0].QuerySelectorAllAsync("td");
    if (tds.Count > 8)
    {
        var btn = await tds[8].QuerySelectorAsync("button");
        if (btn != null) { await btn.ClickAsync(); await Task.Delay(8000); }
    }
} catch { }
Console.WriteLine($"URL: {page.Url}");
await DumpNet("REPORTS");

// Check Vue state
Console.WriteLine("\n=== VUE STATE CHECK ===");
try
{
    var vueCheck = await page.EvaluateAsync("""
        (() => {
            const app = document.querySelector('#app');
            const result = {};
            if (app && app.__vue__) {
                result.vue2 = true;
                result.dataKeys = Object.keys(app.__vue__.$data || {});
                // Try to find store
                if (app.__vue__.$store) {
                    result.storeKeys = Object.keys(app.__vue__.$store.state || {});
                }
            }
            if (app && app.__vue_app__) result.vue3 = true;
            // Check window for any data
            result.windowKeys = Object.keys(window).filter(k => k.includes('data') || k.includes('store') || k.includes('state') || k.includes('app') || k.includes('nuxt')).slice(0, 20);
            return JSON.stringify(result, null, 2);
        })()
    """);
    Console.WriteLine(vueCheck);
} catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }

browser.CloseAsync().GetAwaiter().GetResult();
pw.Dispose();
Console.WriteLine("\nDONE");
