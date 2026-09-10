namespace NezamMonitor.Core.Browser;

/// <summary>
/// Browser-agnostic abstraction for web automation.
/// Implementations can use Playwright, Selenium, or any other driver.
/// </summary>
public interface IWebBrowser : IDisposable
{
    /// <summary>Navigate to URL and wait for DOM ready.</summary>
    Task NavigateAsync(string url, CancellationToken ct = default);

    /// <summary>Click an element matching the given CSS selector.</summary>
    Task ClickAsync(string selector, CancellationToken ct = default);

    /// <summary>Click an element matching the given CSS selector.</summary>
    Task ClickAsync(string selector, int index, CancellationToken ct = default);

    /// <summary>Fill a text input.</summary>
    Task FillAsync(string selector, string value, CancellationToken ct = default);

    /// <summary>Get visible text content of an element.</summary>
    Task<string> GetTextAsync(string selector, CancellationToken ct = default);

    /// <summary>Get visible text content of all matching elements.</summary>
    Task<IReadOnlyList<string>> GetAllTextAsync(string selector, CancellationToken ct = default);

    /// <summary>Evaluate a JavaScript expression and return the result as a string.</summary>
    Task<string> EvaluateAsync(string expression, CancellationToken ct = default);

    /// <summary>Wait for a selector to appear.</summary>
    Task WaitForSelectorAsync(string selector, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>Check if an element matching the selector exists and is visible.</summary>
    Task<bool> IsVisibleAsync(string selector, CancellationToken ct = default);

    /// <summary>Get all elements matching the selector.</summary>
    Task<IReadOnlyList<IBrowserElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);

    /// <summary>Close any open dialogs/overlays by clicking "بستن" buttons.</summary>
    Task CloseDialogsAsync(CancellationToken ct = default);

    /// <summary>Get the current page URL.</summary>
    string Url { get; }

    /// <summary>Get the current page title.</summary>
    string Title { get; }
}

/// <summary>
/// Represents a single DOM element for interaction.
/// </summary>
public interface IBrowserElement
{
    /// <summary>Get the element's visible text.</summary>
    Task<string> GetTextAsync();

    /// <summary>Click the element.</summary>
    Task ClickAsync(CancellationToken ct = default);

    /// <summary>Get the value of an attribute.</summary>
    Task<string?> GetAttributeAsync(string name);

    /// <summary>Get child elements matching a selector.</summary>
    Task<IReadOnlyList<IBrowserElement>> QuerySelectorAllAsync(string selector);

    /// <summary>Get first child element matching a selector.</summary>
    Task<IBrowserElement?> QuerySelectorAsync(string selector);
}
