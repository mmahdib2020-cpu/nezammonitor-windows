using Microsoft.Playwright;

namespace NezamMonitor.Core.Browser;

/// <summary>
/// Playwright-based IWebBrowser implementation using system Edge/Chrome.
/// Does NOT require Playwright-bundled browsers.
/// </summary>
public sealed class PlaywrightBrowser : IWebBrowser
{
    private IPlaywright? _playwright;
    private Microsoft.Playwright.IBrowser? _browser;
    private IPage? _page;
    private bool _disposed;

    public string Url => _page?.Url ?? "";
    public string Title => "";

    public async Task LaunchAsync(
        string? executablePath = null,
        bool headless = false,
        string? userDataDir = null,
        CancellationToken ct = default)
    {
        _playwright = await Playwright.CreateAsync();

        string edgePath = FindBrowserPath();
        if (!string.IsNullOrEmpty(executablePath))
            edgePath = executablePath;

        if (!string.IsNullOrEmpty(userDataDir))
        {
            var ctxOpts = new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = headless,
                ExecutablePath = edgePath,
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Args = new[] { "--no-sandbox", "--disable-blink-features=AutomationControlled" }
            };
            var ctx = await _playwright.Chromium.LaunchPersistentContextAsync(userDataDir, ctxOpts);
            _page = ctx.Pages.Count > 0 ? ctx.Pages[0] : await ctx.NewPageAsync();
        }
        else
        {
            var opts = new BrowserTypeLaunchOptions
            {
                Headless = headless,
                ExecutablePath = edgePath,
                Args = new[] { "--no-sandbox", "--disable-blink-features=AutomationControlled" }
            };
            _browser = await _playwright.Chromium.LaunchAsync(opts);
            var ctx = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });
            _page = await ctx.NewPageAsync();
        }

        _page.SetDefaultTimeout(30000);
    }

    private static string FindBrowserPath()
    {
        string[] candidates =
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        };
        foreach (var p in candidates)
            if (File.Exists(p)) return p;
        return "";
    }

    private void EnsurePage()
    {
        if (_page == null)
            throw new InvalidOperationException("مرورگر راه‌اندازی نشده است.");
    }

    public async Task NavigateAsync(string url, CancellationToken ct = default)
    {
        EnsurePage();
        await _page!.GotoAsync(url, new PageGotoOptions
        {
            Timeout = 60000,
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
    }

    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        EnsurePage();
        await _page!.ClickAsync(selector, new PageClickOptions { Force = true });
    }

    public async Task ClickAsync(string selector, int index, CancellationToken ct = default)
    {
        EnsurePage();
        var els = await _page!.QuerySelectorAllAsync(selector);
        if (index < els.Count)
            await els[index].ClickAsync(new ElementHandleClickOptions { Force = true });
    }

    public async Task FillAsync(string selector, string value, CancellationToken ct = default)
    {
        EnsurePage();
        await _page!.FillAsync(selector, value);
    }

    public async Task<string> GetTextAsync(string selector, CancellationToken ct = default)
    {
        EnsurePage();
        var el = await _page!.QuerySelectorAsync(selector);
        return el != null ? await el.InnerTextAsync() : "";
    }

    public async Task<IReadOnlyList<string>> GetAllTextAsync(string selector, CancellationToken ct = default)
    {
        EnsurePage();
        var els = await _page!.QuerySelectorAllAsync(selector);
        var results = new List<string>();
        foreach (var el in els)
            results.Add(await el.InnerTextAsync());
        return results;
    }

    public async Task<string> EvaluateAsync(string expression, CancellationToken ct = default)
    {
        EnsurePage();
        return await _page!.EvaluateAsync<string>(expression);
    }

    public async Task WaitForSelectorAsync(string selector, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        EnsurePage();
        await _page!.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
        {
            Timeout = (int)(timeout ?? TimeSpan.FromSeconds(30)).TotalMilliseconds
        });
    }

    public async Task<bool> IsVisibleAsync(string selector, CancellationToken ct = default)
    {
        EnsurePage();
        var el = await _page!.QuerySelectorAsync(selector);
        return el != null && await el.IsVisibleAsync();
    }

    public async Task<IReadOnlyList<IBrowserElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default)
    {
        EnsurePage();
        var els = await _page!.QuerySelectorAllAsync(selector);
        return els.Select(e => (IBrowserElement)new PlaywrightElement(e)).ToList();
    }

    public async Task CloseDialogsAsync(CancellationToken ct = default)
    {
        EnsurePage();
        for (int i = 0; i < 5; i++)
        {
            bool closed = false;
            try
            {
                var buttons = await _page!.QuerySelectorAllAsync("button");
                foreach (var btn in buttons)
                {
                    var text = (await btn.InnerTextAsync()).Trim();
                    var visible = await btn.IsVisibleAsync();
                    if (text == "\u0628\u0633\u062a\u0646" && visible)
                    {
                        await btn.ClickAsync(new ElementHandleClickOptions { Force = true });
                        await Task.Delay(1000);
                        closed = true;
                        break;
                    }
                }
            }
            catch { }
            if (!closed) break;
            await Task.Delay(500);
        }
    }

    public Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        EnsurePage();
        return Task.FromResult(!_page!.Url.Contains("login"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _browser?.CloseAsync().GetAwaiter().GetResult(); } catch { }
        try { _playwright?.Dispose(); } catch { }
    }
}

/// <summary>
/// Wraps a Playwright IElementHandle as an IBrowserElement.
/// </summary>
internal sealed class PlaywrightElement : IBrowserElement
{
    private readonly IElementHandle _element;
    public PlaywrightElement(IElementHandle element) => _element = element;

    public async Task<string> GetTextAsync()
    {
        try { return await _element.InnerTextAsync(); } catch { return ""; }
    }

    public async Task ClickAsync(CancellationToken ct = default)
    {
        try { await _element.ClickAsync(new ElementHandleClickOptions { Force = true }); } catch { }
    }

    public async Task<string?> GetAttributeAsync(string name)
    {
        try { return await _element.GetAttributeAsync(name); } catch { return null; }
    }

    public async Task<IReadOnlyList<IBrowserElement>> QuerySelectorAllAsync(string selector)
    {
        try
        {
            var els = await _element.QuerySelectorAllAsync(selector);
            return els.Select(e => (IBrowserElement)new PlaywrightElement(e)).ToList();
        }
        catch { return new List<IBrowserElement>(); }
    }

    public async Task<IBrowserElement?> QuerySelectorAsync(string selector)
    {
        try
        {
            var el = await _element.QuerySelectorAsync(selector);
            return el != null ? new PlaywrightElement(el) : null;
        }
        catch { return null; }
    }
}
