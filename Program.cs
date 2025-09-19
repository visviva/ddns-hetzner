using System.CommandLine;
using DdnsHetzner;
using DotNetEnv;
using ErrorOr;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Enable verbose output"
        };

        var createEnvOption = new Option<bool>("--create-env-file")
        {
            Description = "Create a sample .env file and exit"
        };

        var rootCommand = new RootCommand("Hetzner Dynamic DNS (DDNS) client")
        {
            verboseOption,
            createEnvOption
        };

        rootCommand.SetAction(async parseResult =>
        {
            bool verbose = parseResult.GetValue(verboseOption);
            bool createEnv = parseResult.GetValue(createEnvOption);

            if (createEnv)
            {
                await CreateSampleEnvFile();
                return 0;
            }

            await RunDdnsUpdate(verbose);
            return 0;
        });

        ParseResult parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }

    private static async Task CreateSampleEnvFile()
    {
        const string sampleEnvContent = """
            # Hetzner DDNS Configuration
            # URL to get your public IPv4 address
            IPV4_URL=https://ipv4.icanhazip.com

            # Your domain name (the zone in Hetzner DNS)
            DOMAIN=example.com

            # The subdomain/hostname to update (e.g., "home" for home.example.com)
            SUBDOMAIN=home

            # Your Hetzner DNS API token
            # Get this from: https://dns.hetzner.com/settings/api-token
            TOKEN=your_hetzner_api_token_here
            """;

        const string envFileName = ".env";

        if (File.Exists(envFileName))
        {
            Console.WriteLine($"⚠️  {envFileName} already exists. Do you want to overwrite it? (y/N)");
            var response = Console.ReadLine()?.ToLower();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Cancelled. Existing .env file was not modified.");
                return;
            }
        }

        await File.WriteAllTextAsync(envFileName, sampleEnvContent);
        Console.WriteLine($"✅ Created sample {envFileName} file.");
        Console.WriteLine("Please edit the file and add your actual Hetzner API token and domain settings.");
    }

    private static async Task RunDdnsUpdate(bool verbose)
    {
        Env.Load();
        var envOrError = CheckEnv();
        if (envOrError.IsError)
        {
            Console.WriteLine($"❌ Validation of .env file failed: {envOrError.FirstError.Description}");
            Environment.Exit(1);
        }

        var env = envOrError.Value;

        if (verbose)
        {
            Console.WriteLine("🔧 Verbose mode enabled");
            Console.WriteLine($"📍 Domain: {env.Domain}");
            Console.WriteLine($"🏠 Subdomain: {env.Subdomain}");
            Console.WriteLine($"🌐 IPv4 URL: {env.Ipv4Url}");
        }

        using var httpClient = new HttpClient();

        while (true)
        {
            Console.WriteLine($"\n=== {DateTime.Now} ===");
            Console.WriteLine("Starting DDNS update process...");

            string publicIpv4 = string.Empty;

            await GetPublicIpv4(env.Ipv4Url, httpClient)
                .ThenAsync(ipv4 =>
                {
                    var task = Task.FromResult(ipv4);
                    Console.WriteLine($"Fetched public IPv4 address {ipv4}");
                    return task;
                })
                .Then<string, string>(ipv4 =>
                {
                    if (publicIpv4 == ipv4)
                    {
                        return Error.Custom(description: "IP address has not changed. No update needed.", type: 0, code: "NoUpdateNeeded");
                    }
                    publicIpv4 = ipv4;
                    return ipv4;
                })
                .ThenAsync(ipv4 => GetZones(env.Token, httpClient, verbose)
                    .Then(zones => (ipv4, zones)))
                .Then(tuple => GetZoneIdByName(tuple.zones, env.Domain)
                    .Then(zoneId => (tuple.ipv4, zoneId)))
                .ThenAsync(tuple => GetRecordsForZone(tuple.zoneId, env.Token, httpClient, verbose)
                    .Then(records => (tuple.ipv4, tuple.zoneId, records)))
                .Then(tuple => GetRecordByName(tuple.records, env.Subdomain)
                    .Then(record => (tuple.ipv4, tuple.zoneId, record)))
                .ThenAsync(tuple =>
                        UpdateRecord(tuple.record.Id, tuple.zoneId, tuple.record.Type, tuple.record.Name, tuple.ipv4, 7200, env.Token, httpClient, verbose)
                    .Then(result => (tuple.ipv4, result)))
                .MatchFirst(
                    result =>
                    {
                        Console.WriteLine("✅ DNS record updated successfully");
                        return true;
                    },
                    error =>
                    {
                        if (error.Type == 0 && error.Code == "NoUpdateNeeded")
                        {
                            Console.WriteLine("ℹ️ " + error.Description);
                        }

                        Console.WriteLine($"\nError: {error}");
                        Console.WriteLine("❌ Failed to update DNS record.");
                        return false;
                    }
                );

            await Task.Delay(TimeSpan.FromMinutes(10));
        }

    }

    record struct EnvInfo(string Ipv4Url, string Token, string Domain, string Subdomain);

    private static ErrorOr<EnvInfo> CheckEnv()
    {
        var ipv4Url = Environment.GetEnvironmentVariable("IPV4_URL");
        if (ipv4Url is null or "")
        {
            Console.WriteLine("IPV4_URL environment variable not found");
            return Error.Validation(description: "IPV4_URL environment variable not found");
        }

        var token = Environment.GetEnvironmentVariable("TOKEN");
        if (token is null or "")
        {
            Console.WriteLine("TOKEN environment variable not found");
            return Error.Validation(description: "TOKEN environment variable not found");
        }

        var domain = Environment.GetEnvironmentVariable("DOMAIN");
        if (domain is null or "")
        {
            Console.WriteLine("DOMAIN environment variable not found");
            return Error.Validation(description: "DOMAIN environment variable not found");
        }

        var subdomain = Environment.GetEnvironmentVariable("SUBDOMAIN");
        if (subdomain is null or "")
        {
            Console.WriteLine("SUBDOMAIN environment variable not found");
            return Error.Validation(description: "SUBDOMAIN environment variable not found");
        }

        return new EnvInfo(ipv4Url, token, domain, subdomain);
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

    private static async Task<ErrorOr<ZoneList>> GetZones(string token, HttpClient httpClient, bool verbose)
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

    private static async Task<ErrorOr<RecordList>> GetRecordsForZone(string zoneId, string token, HttpClient httpClient, bool verbose)
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

    private static async Task<ErrorOr<Success>> UpdateRecord(string recordId, string zoneId, string type, string name, string value, int? ttl, string token, HttpClient httpClient, bool verbose)
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
