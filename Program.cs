using System;
using System.Net.Http;
using System.Threading.Tasks;
using DdnsHetzner;
using DotNetEnv;
using ErrorOr;

class Program
{
    static async Task Main(string[] args)
    {
        Env.Load();

        var ipv4Url = Environment.GetEnvironmentVariable("IPV4_URL");
        if (ipv4Url is null or "")
        {
            Console.WriteLine("IPV4_URL environment variable not found");
            return;
        }

        var token = Environment.GetEnvironmentVariable("TOKEN");
        if (token is null or "")
        {
            Console.WriteLine("TOKEN environment variable not found");
            return;
        }

        var domain = Environment.GetEnvironmentVariable("DOMAIN");
        if (domain is null or "")
        {
            Console.WriteLine("DOMAIN environment variable not found");
            return;
        }

        var subdomain = Environment.GetEnvironmentVariable("SUBDOMAIN");
        if (subdomain is null or "")
        {
            Console.WriteLine("SUBDOMAIN environment variable not found");
            return;
        }

        using var httpClient = new HttpClient();

        var x = await GetPublicIpv4(ipv4Url, httpClient)
            .ThenAsync(ipv4 =>
            {
                var task = Task.FromResult(ipv4);
                Console.WriteLine($"Fetched public IPv4 address {ipv4}");
                return task;
            })
            .ThenAsync(ipv4 => GetZones(token, httpClient)
                .Then(zones => (ipv4, zones)))
            .Then(tuple => GetZoneIdByName(tuple.zones, domain)
                .Then(zoneId => (tuple.ipv4, zoneId)))
            .ThenAsync(tuple => GetRecordsForZone(tuple.zoneId, token, httpClient)
                .Then(records => (tuple.ipv4, tuple.zoneId, records)))
            .Then(tuple => GetRecordByName(tuple.records, subdomain)
                .Then(record => (tuple.ipv4, tuple.zoneId, record)))
            .ThenAsync(tuple =>
                    UpdateRecord(tuple.record.Id, tuple.zoneId, tuple.record.Type, tuple.record.Name, tuple.ipv4, 7200, token, httpClient))
            .MatchFirst(
                result =>
                {
                    Console.WriteLine("Record updated successfully.");
                    return true;
                },
                error =>
                {
                    Console.WriteLine($"\nError: {error}");
                    return false;
                }
            );
    }

    private static async Task<ErrorOr<string>> GetPublicIpv4(string ipv4Url, HttpClient httpClient)
    {
        try
        {
            string ipv4 = await httpClient.GetStringAsync(ipv4Url);
            return ipv4;
        }
        catch (Exception ex)
        {
            return Error.Unexpected(description: $"Failed to fetch IPv4: {ex.Message}");
        }
    }

    private static async Task<ErrorOr<ZoneList>> GetZones(string token, HttpClient httpClient)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://dns.hetzner.com/api/v1/zones");
            request.Headers.Add("Auth-API-Token", token);

            var response = await httpClient.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"\nHetzner DNS API Response:");
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Content: {result}");

            ZoneList? zones = System.Text.Json.JsonSerializer.Deserialize<ZoneList>(result);
            if (zones is null)
            {
                return Error.Unexpected(description: "Failed to deserialize Hetzner DNS API response");
            }

            return zones;
        }
        catch (Exception ex)
        {
            return Error.Unexpected(description: $"Failed to call Hetzner DNS API: {ex.Message}");
        }
    }

    private static ErrorOr<string> GetZoneIdByName(ZoneList zones, string zoneName)
    {
        foreach (var zone in zones.Zones)
        {
            if (zone.Name == zoneName)
            {
                return zone.Id;
            }
        }
        return Error.NotFound(description: $"Zone with name '{zoneName}' not found");
    }

    private static async Task<ErrorOr<RecordList>> GetRecordsForZone(string zoneId, string token, HttpClient httpClient)
    {
        try
        {
            var uriBuilder = new UriBuilder("https://dns.hetzner.com/api/v1/records");
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            query["zone_id"] = zoneId;
            uriBuilder.Query = query.ToString();
            var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Add("Auth-API-Token", token);

            var response = await httpClient.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"\nHetzner DNS API Response:");
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Content: {result}");

            var records = System.Text.Json.JsonSerializer.Deserialize<RecordList>(result);
            if (records is null)
            {
                return Error.Unexpected(description: "Failed to deserialize Hetzner DNS API response");
            }

            return records;
        }
        catch (Exception ex)
        {
            return Error.Unexpected(description: $"Failed to call Hetzner DNS API: {ex.Message}");
        }
    }

    private static ErrorOr<Record> GetRecordByName(RecordList records, string recordName)
    {
        foreach (var record in records.Records)
        {
            if (record.Name == recordName)
            {
                return record;
            }
        }
        return Error.NotFound(description: $"Record with name '{recordName}' not found");
    }

    private static async Task<ErrorOr<Success>> UpdateRecord(string recordId, string zoneId, string type, string name, string value, int? ttl, string token, HttpClient httpClient)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"https://dns.hetzner.com/api/v1/records/{recordId}");
            request.Headers.Add("Auth-API-Token", token);

            var recordUpdate = new DnsRecord(zoneId, Enum.Parse<DnsRecordType>(type), name, value, ttl);
            string jsonContent = System.Text.Json.JsonSerializer.Serialize(recordUpdate);
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"\nHetzner DNS API Response:");
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Content: {result}");

            if (!response.IsSuccessStatusCode)
            {
                return Error.Unexpected(description: $"Failed to update record: {result}");
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Unexpected(description: $"Failed to call Hetzner DNS API: {ex.Message}");
        }
    }
}
