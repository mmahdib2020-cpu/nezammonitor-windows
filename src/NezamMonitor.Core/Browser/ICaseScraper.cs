using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Browser;

/// <summary>
/// Scrapes case data from the Nezam Engineering website.
/// </summary>
public interface ICaseScraper
{
    /// <summary>Login with credentials.</summary>
    Task<bool> LoginAsync(string username, string password, CancellationToken ct = default);

    /// <summary>Navigate to the case monitoring table.</summary>
    Task NavigateToTableAsync(CancellationToken ct = default);

    /// <summary>Get all cases from the current table view.</summary>
    Task<IReadOnlyList<Case>> ScrapeAllCasesAsync(
        Action<string>? statusCallback = null,
        CancellationToken ct = default);

    /// <summary>Get detailed info for a single case (engineers, specs, fees, reports).</summary>
    Task<Case> ScrapeCaseDetailsAsync(Case basicCase, CancellationToken ct = default);

    /// <summary>Check if the scraper is logged in.</summary>
    Task<bool> IsLoggedInAsync(CancellationToken ct = default);

    /// <summary>Close any open dialogs/overlays.</summary>
    Task CloseDialogsAsync(CancellationToken ct = default);
}
