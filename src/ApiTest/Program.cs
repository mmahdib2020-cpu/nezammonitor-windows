using System.Text;
using System.Text.Json;

var baseUrl = "http://service.yazdnezam.ir:8033";
var username = args[0];
var password = args[1];

using var http = new HttpClient();
http.Timeout = TimeSpan.FromSeconds(30);

// Login
var loginPayload = JsonSerializer.Serialize(new { ozv_num = username, ozv_pass = password, ozv_type = 0 });
var loginReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/panel/api/login")
{
    Content = new StringContent(loginPayload, Encoding.UTF8, "application/json")
};
var loginResp = await http.SendAsync(loginReq);
var loginJson = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync());
var token = loginJson.RootElement.GetProperty("token").GetString()!;

// Get user
http.DefaultRequestHeaders.Add("Authorization", token);
var userResp = await http.GetAsync($"{baseUrl}/panel/api/user");
var userJson = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());
var userId = userJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();
var cityId = userJson.RootElement.GetProperty("user").GetProperty("user_shahrestan").GetInt32();
Console.WriteLine($"userId={userId}, cityId={cityId}");

// Get ALL cases
var caseResp = await http.GetAsync($"{baseUrl}/panel/api/showParvandeNezaratMeybod/{userId}/{cityId}");
var caseBody = await caseResp.Content.ReadAsStringAsync();
var cases = JsonDocument.Parse(caseBody);
Console.WriteLine($"Total cases: {cases.RootElement.GetArrayLength()}");

// Show ALL fields of first case
Console.WriteLine("\n=== FIRST CASE — ALL FIELDS ===");
var first = cases.RootElement[0];
foreach (var prop in first.EnumerateObject())
{
    Console.WriteLine($"  {prop.Name} = {prop.Value}");
}

// Get all unique field names across all cases
Console.WriteLine("\n=== ALL FIELD NAMES (union) ===");
var allFields = new SortedSet<string>();
foreach (var c in cases.RootElement.EnumerateArray())
{
    foreach (var prop in c.EnumerateObject())
        allFields.Add(prop.Name);
}
foreach (var f in allFields) Console.WriteLine($"  {f}");

// Get engineers for first case
var dbId = first.GetProperty("db_id").GetInt32();
Console.WriteLine($"\n=== ENGINEERS for dbId={dbId} ===");
var engResp = await http.GetAsync($"{baseUrl}/panel/api/showNazer/{dbId}/{cityId}");
var engBody = await engResp.Content.ReadAsStringAsync();
var engJson = JsonDocument.Parse(engBody);
if (engJson.RootElement.TryGetProperty(dbId.ToString(), out var engArray))
{
    Console.WriteLine($"Count: {engArray.GetArrayLength()}");
    if (engArray.GetArrayLength() > 0)
    {
        Console.WriteLine("Fields:");
        foreach (var prop in engArray[0].EnumerateObject())
            Console.WriteLine($"  {prop.Name} = {prop.Value}");
    }
}

// Get fees for first case
Console.WriteLine($"\n=== FEES for dbId={dbId} ===");
var feePayload = JsonSerializer.Serialize(new { db_id = dbId.ToString(), sha_id = cityId.ToString(), ozv_id = userId.ToString() });
var feeReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/panel/api/showMali")
{
    Content = new StringContent(feePayload, Encoding.UTF8, "application/json")
};
var feeResp = await http.SendAsync(feeReq);
var feeBody = await feeResp.Content.ReadAsStringAsync();
var feeJson = JsonDocument.Parse(feeBody);
Console.WriteLine($"Count: {feeJson.RootElement.GetArrayLength()}");
if (feeJson.RootElement.GetArrayLength() > 0)
{
    Console.WriteLine("Fields:");
    foreach (var prop in feeJson.RootElement[0].EnumerateObject())
        Console.WriteLine($"  {prop.Name} = {prop.Value}");
}

// Check if reports/specs are in the case list data
Console.WriteLine("\n=== CASE LIST — check for report/spec fields ===");
var reportFields = first.EnumerateObject().Where(p =>
    p.Name.Contains("report") || p.Name.Contains("marhale") || p.Name.Contains("gozarsh") ||
    p.Name.Contains("spec") || p.Name.Contains("sazeh") || p.Name.Contains("tabaghat") ||
    p.Name.Contains("vahed") || p.Name.Contains("block") || p.Name.Contains("address") ||
    p.Name.Contains("shahr") || p.Name.Contains("zamin") || p.Name.Contains("eteha") ||
    p.Name.Contains("tarh") || p.Name.Contains("karبري") || p.Name.Contains(" UsageType") ||
    p.Name.Contains("karbord")).ToList();
foreach (var f in reportFields) Console.WriteLine($"  {f.Name} = {f.Value}");

Console.WriteLine("\nDONE");
