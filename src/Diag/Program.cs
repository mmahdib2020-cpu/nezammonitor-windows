using NezamMonitor.Core.Browser;

if (args.Length < 2) { Console.WriteLine("Usage: dotnet run -- <user> <pass>"); return; }

var browser = new PlaywrightBrowser();
await browser.LaunchAsync(headless: false);
var scraper = new CaseScraper(browser);

var ok = await scraper.LoginAsync(args[0], args[1]);
Console.WriteLine($"Login: {(ok ? "OK" : "FAILED")}");
if (!ok) { browser.Dispose(); return; }

await Task.Delay(3000);

// Navigate to table
await browser.NavigateAsync("http://service.yazdnezam.ir:8033/panel/dashboard/khadamat/parvandeNezarat");
await Task.Delay(8000);

// Close the persistent dialog first
Console.WriteLine("=== CLOSING PERSISTENT DIALOG ===");
try
{
    var closeBtns = await browser.QuerySelectorAllAsync(".v-dialog--active button");
    foreach (var btn in closeBtns)
    {
        var text = await btn.GetTextAsync();
        if (text.Contains("بستن"))
        {
            await btn.ClickAsync();
            Console.WriteLine("Clicked بستن");
            await Task.Delay(2000);
            break;
        }
    }
}
catch (Exception ex) { Console.WriteLine($"Dialog close error: {ex.Message}"); }

// Select Ardakan
Console.WriteLine("=== SELECTING ARDAKAN ===");
try
{
    var clicked = await browser.EvaluateAsync("""
        (() => {
            const labels = document.querySelectorAll('.v-input--radio-group .v-radio label, .v-input--selection-controls label');
            for (const l of labels) {
                if (l.textContent.trim().includes('اردكان') || l.textContent.trim().includes('اردکان')) {
                    l.click();
                    return 'clicked_label';
                }
            }
            return 'not_found';
        })()
    """);
    Console.WriteLine($"Ardakan: {clicked}");
    await Task.Delay(2000);
}
catch (Exception ex) { Console.WriteLine($"Ardakan error: {ex.Message}"); }

// Click Monitoring tab
Console.WriteLine("=== CLICKING MONITORING TAB ===");
try
{
    await browser.EvaluateAsync("""
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
    """);
    await Task.Delay(5000);
}
catch (Exception ex) { Console.WriteLine($"Tab error: {ex.Message}"); }

Console.WriteLine($"\n=== TABLE STATUS ===");
Console.WriteLine($"URL: {browser.Url}");
var rows = await browser.QuerySelectorAllAsync("table tbody tr");
Console.WriteLine($"Rows: {rows.Count}");

if (rows.Count == 0)
{
    Console.WriteLine("Still no rows! Checking page state...");
    var text = await browser.EvaluateAsync("document.body.innerText");
    Console.WriteLine($"Page text: {text?.Substring(0, Math.Min(text?.Length ?? 0, 500))}");
    browser.Dispose();
    return;
}

var firstRow = rows[0];
var tds = await firstRow.QuerySelectorAllAsync("td");
var serial = (await tds[0].GetTextAsync()).Trim();
Console.WriteLine($"First serial: {serial}, cells: {tds.Count}");

// === ENGINEERS DIALOG ===
if (tds.Count > 10)
{
    var engBtn = await tds[10].QuerySelectorAsync("button");
    if (engBtn != null)
    {
        Console.WriteLine("\n=== CLICKING ENGINEERS BUTTON ===");
        await engBtn.ClickAsync(ct: default);
        await Task.Delay(3000, default);

        Console.WriteLine("\n=== ENGINEERS DIALOG DOM ===");
        var engDom = await browser.EvaluateAsync("""
            (() => {
                const dialog = document.querySelector('.v-dialog--active');
                if (!dialog) return JSON.stringify({error: 'no active dialog'});
                
                const result = {
                    innerHTML: dialog.innerHTML.substring(0, 4000),
                    textNodes: [],
                };
                
                const allElements = dialog.querySelectorAll('*');
                allElements.forEach(el => {
                    if (el.children.length === 0 && el.textContent.trim().length > 0) {
                        result.textNodes.push({
                            tag: el.tagName,
                            cls: el.className.substring(0, 80),
                            text: el.textContent.trim().substring(0, 150),
                        });
                    }
                });
                
                return JSON.stringify(result, null, 2);
            })()
        """);
        Console.WriteLine(engDom);

        // Close engineers dialog
        try
        {
            var btns = await browser.QuerySelectorAllAsync(".v-dialog--active button");
            foreach (var b in btns)
            {
                var t = await b.GetTextAsync();
                if (t.Contains("بستن")) { await b.ClickAsync(); break; }
            }
        }
        catch { }
        await Task.Delay(2000);
        if (browser.Url.Contains("/mali/"))
        {
            await browser.NavigateAsync("http://service.yazdnezam.ir:8033/panel/dashboard/khadamat/parvandeNezarat");
            await Task.Delay(5000);
        }
    }
}

// === SPECS DIALOG ===
rows = await browser.QuerySelectorAllAsync("table tbody tr");
if (rows.Count > 0)
{
    var r = rows[0];
    var cells = await r.QuerySelectorAllAsync("td");
    if (cells.Count > 11)
    {
        var specBtn = await cells[11].QuerySelectorAsync("button");
        if (specBtn != null)
        {
            Console.WriteLine("\n=== CLICKING SPECS BUTTON ===");
            await specBtn.ClickAsync(ct: default);
            await Task.Delay(3000, default);

            Console.WriteLine("\n=== SPECS DIALOG DOM ===");
            var specDom = await browser.EvaluateAsync("""
                (() => {
                    const dialog = document.querySelector('.v-dialog--active');
                    if (!dialog) return JSON.stringify({error: 'no active dialog'});
                    
                    const result = {
                        innerHTML: dialog.innerHTML.substring(0, 5000),
                        textNodes: [],
                    };
                    
                    const allElements = dialog.querySelectorAll('*');
                    allElements.forEach(el => {
                        if (el.children.length === 0 && el.textContent.trim().length > 0) {
                            result.textNodes.push({
                                tag: el.tagName,
                                cls: el.className.substring(0, 80),
                                text: el.textContent.trim().substring(0, 150),
                            });
                        }
                    });
                    
                    return JSON.stringify(result, null, 2);
                })()
            """);
            Console.WriteLine(specDom);
        }
    }
}

Console.WriteLine("\nDONE");
browser.Dispose();
