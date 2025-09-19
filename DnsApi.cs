using ErrorOr;

namespace DdnsHetzner;

public class DnsApi(HttpClient httpClient, string token, bool verbose)
{

    public ErrorOr<Record> GetRecordByName(RecordList records, string recordName)
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

    public async Task<ErrorOr<RecordList>> GetRecordsForZone(string zoneId)
    {
        try
        {
            Console.WriteLine("Start fetching Records...");
            var uriBuilder = new UriBuilder("https://dns.hetzner.com/api/v1/records");
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            query["zone_id"] = zoneId;
            uriBuilder.Query = query.ToString();
            var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Add("Auth-API-Token", token);

            var response = await httpClient.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                Console.WriteLine(
                $"""
                Hetzner DNS API Response:
                Status: {response.StatusCode}
                Content: {result}
                """);
            }

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ Fetch Records\n");
            }
            else
            {
                Console.WriteLine("❌ Fetch Records\n");
                return Error.Unexpected(description: $"Failed to call Hetzner DNS API: {response.StatusCode}");
            }

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

    public ErrorOr<string> GetZoneIdByName(ZoneList zones, string zoneName)
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

    public async Task<ErrorOr<ZoneList>> GetZones()
    {
        try
        {
            Console.WriteLine("Start fetching Zones...");
            var request = new HttpRequestMessage(HttpMethod.Get, "https://dns.hetzner.com/api/v1/zones");
            request.Headers.Add("Auth-API-Token", token);

            var response = await httpClient.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                Console.WriteLine(
                $"""
                Hetzner DNS API Response:
                Status: {response.StatusCode}
                Content: {result}
                """);
            }

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ Fetch Zones\n");
            }
            else
            {
                Console.WriteLine("❌ Fetch Zones\n");
                return Error.Unexpected(description: $"Failed to call Hetzner DNS API: {response.StatusCode}");
            }


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

    public async Task<ErrorOr<Success>> UpdateRecord(string recordId, string zoneId, string type, string name, string value, int? ttl)
    {
        try
        {
            Console.WriteLine("Start updating Record...");
            var request = new HttpRequestMessage(HttpMethod.Put, $"https://dns.hetzner.com/api/v1/records/{recordId}");
            request.Headers.Add("Auth-API-Token", token);

            var recordUpdate = new DnsRecord(zoneId, Enum.Parse<DnsRecordType>(type), name, value, ttl);
            string jsonContent = System.Text.Json.JsonSerializer.Serialize(recordUpdate);
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                Console.WriteLine($"\nHetzner DNS API Response:");
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Content: {result}");
            }

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ Update Record\n");
            }
            else
            {
                Console.WriteLine("❌ Update Record\n");
                return Error.Unexpected(description: $"Failed to call Hetzner DNS API: {response.StatusCode}");
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Unexpected(description: $"Failed to call Hetzner DNS API: {ex.Message}");
        }
    }
}
