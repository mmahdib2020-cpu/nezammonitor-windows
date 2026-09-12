using System.Text;
using System.Text.Json;

var username = args[0];
var password = args[1];
var baseUrl = "http://service.yazdnezam.ir:8033";
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

var payload = JsonSerializer.Serialize(new { ozv_num = username, ozv_pass = password, ozv_type = 0 });
var loginReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/panel/api/login")
{ Content = new StringContent(payload, Encoding.UTF8, "application/json") };
var loginResp = await http.SendAsync(loginReq);
var token = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString()!;
http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(token);

var userResp = await http.GetAsync($"{baseUrl}/panel/api/user");
var userJson = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());
var userId = userJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();
var cityId = userJson.RootElement.GetProperty("user").GetProperty("user_shahrestan").GetInt32();

var caseResp = await http.GetAsync($"{baseUrl}/panel/api/showParvandeNezaratMeybod/{userId}/{cityId}");
var cases = JsonDocument.Parse(await caseResp.Content.ReadAsStringAsync());
var dbId = cases.RootElement[0].GetProperty("db_id").GetInt32();

var feePayload = JsonSerializer.Serialize(new { db_id = dbId.ToString(), sha_id = cityId.ToString(), ozv_id = userId.ToString() });
var feeReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/panel/api/showMali")
{ Content = new StringContent(feePayload, Encoding.UTF8, "application/json") };
var feeResp = await http.SendAsync(feeReq);
var feeJson = JsonDocument.Parse(await feeResp.Content.ReadAsStringAsync());

Console.WriteLine("=== FEE FIELDS ===");
foreach (var fee in feeJson.RootElement.EnumerateArray())
{
    Console.WriteLine($"payed={S(fee, "payed")} selected={S(fee, "selected")} mablagh={S(fee, "dbm_mablagh")} marhala={S(fee, "dbm_marhala_id")} mas={S(fee, "mas_title")}");
}

Console.WriteLine("\n=== CASE DATE FIELDS ===");
var fc = cases.RootElement[0];
Console.WriteLine($"das_date={S(fc, "das_date")}");
Console.WriteLine($"das_date_tarkhis={S(fc, "das_date_tarkhis")}");
Console.WriteLine($"kasr_date={S(fc, "kasr_date")}");
Console.WriteLine($"das_parvana_date={S(fc, "das_parvana_date")}");

Console.WriteLine("\nDONE");
static string S(JsonElement el, string p) { if (el.TryGetProperty(p, out var v)) return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString(); return ""; }
