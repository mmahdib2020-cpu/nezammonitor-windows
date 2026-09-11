using System.Diagnostics;
using NezamMonitor.Core.Api;
using NezamMonitor.Core.Models;

if (args.Length < 2) { Console.WriteLine("Usage: dotnet run -- <user> <pass> [maxCases]"); return; }

var username = args[0];
var password = args[1];
int maxCases = args.Length > 2 ? int.Parse(args[2]) : 0;

Console.WriteLine("=== API EXTRACTION BENCHMARK ===");
Console.WriteLine($"Max cases: {(maxCases == 0 ? "ALL" : maxCases.ToString())}");
Console.WriteLine();

var totalSw = Stopwatch.StartNew();

// LOGIN
Console.WriteLine("[1] Login...");
var loginSw = Stopwatch.StartNew();
using var api = new NezamApiClient();
var loginOk = await api.LoginAsync(username, password);
loginSw.Stop();
Console.WriteLine($"    Login: {(loginOk ? "OK" : "FAILED")} ({loginSw.ElapsedMilliseconds}ms)");
if (!loginOk) return;
Console.WriteLine($"    userId={api.UserId}, cityId={api.CityId}");

// GET CASES
Console.WriteLine("[2] Getting case list...");
var caseSw = Stopwatch.StartNew();
var apiCases = await api.GetCasesRawAsync();
caseSw.Stop();
Console.WriteLine($"    Cases: {apiCases.Count} ({caseSw.ElapsedMilliseconds}ms)");

// GET ENGINEERS + FEES
Console.WriteLine($"[3] Getting engineers + fees...");
var extractSw = Stopwatch.StartNew();
var allCases = new List<Case>();
int engTotal = 0, feeTotal = 0, errors = 0;

var casesToProcess = maxCases > 0 ? apiCases.Take(maxCases).ToList() : apiCases;
foreach (var apiCase in casesToProcess)
{
    try
    {
        var caseModel = ApiExtractor.MapCase(apiCase);

        var dbId = 0;
        if (apiCase.TryGetValue("db_id", out var dbVal) && dbVal is int dbI) dbId = dbI;

        var apiEngineers = await api.GetEngineersRawAsync(dbId);
        caseModel.Engineers = ApiExtractor.MapEngineers(apiEngineers);
        engTotal += caseModel.Engineers.Count;

        var apiFees = await api.GetFeesRawAsync(dbId);
        caseModel.Fees = ApiExtractor.MapFees(apiFees);
        feeTotal += caseModel.Fees.Count;

        allCases.Add(caseModel);
    }
    catch (Exception ex)
    {
        errors++;
        Console.WriteLine($"    FAIL: {NezamApiClient.S(apiCase, "das_serial")} - {ex.Message}");
    }
}
extractSw.Stop();
totalSw.Stop();

Console.WriteLine();
Console.WriteLine("=== RESULTS ===");
Console.WriteLine($"Total time: {totalSw.ElapsedMilliseconds}ms ({totalSw.Elapsed.TotalSeconds:F1}s)");
Console.WriteLine($"Login: {loginSw.ElapsedMilliseconds}ms");
Console.WriteLine($"Case list: {caseSw.ElapsedMilliseconds}ms");
Console.WriteLine($"Extract (eng+fees): {extractSw.ElapsedMilliseconds}ms ({extractSw.Elapsed.TotalSeconds:F1}s)");
Console.WriteLine();
Console.WriteLine($"Cases: {allCases.Count}");
Console.WriteLine($"Engineers total: {engTotal}");
Console.WriteLine($"Fees total: {feeTotal}");
Console.WriteLine($"Errors: {errors}");

if (allCases.Count > 0)
{
    Console.WriteLine($"Avg per case: {extractSw.ElapsedMilliseconds / allCases.Count}ms");
    Console.WriteLine();
    foreach (var c in allCases.Take(5))
    {
        var spec = c.Specification;
        Console.WriteLine($"{c.CaseNumber} | {c.Owner} | Eng:{c.Engineers.Count} Fees:{c.Fees.Count} Spec:{(spec != null ? "Y" : "N")} Usage:{spec?.UsageType ?? "EMPTY"}");
        if (spec != null)
            Console.WriteLine($"  Group={spec.BuildingGroup} Struct={spec.StructureType} Floors={spec.Floors} Permit={spec.PermitNumber} Cap={spec.CapacityArea} Addr={spec.Address}");
    }
}

Console.WriteLine("\nDONE");
