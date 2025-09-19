using System.CommandLine;
using DdnsHetzner;
using DotNetEnv;
using ErrorOr;
using System.Net;
using System.Text.Json;

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

            #TTL for the DNS record in seconds (optional, default is 7200)
            TTL=7200

            # Your Hetzner DNS API token
            # Get this from: https://dns.hetzner.com/settings/api-token
            TOKEN=your_hetzner_api_token_here

            # Interval in minutes between IP checks and updates (optional, default is 10)
            INTERVAL=10

            # Port for health check API (optional, default is 8080, set to 0 to disable)
            HEALTH_PORT=8080
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
        // Only load .env file if required environment variables are not already set
        bool envFileLoaded = false;
        if (!AreRequiredEnvVarsPresent())
        {
            Env.Load();
            envFileLoaded = true;
        }

        var envOrError = CheckEnv();
        if (envOrError.IsError)
        {
            Console.WriteLine($"❌ Environment variable validation failed: {envOrError.FirstError.Description}");
            Environment.Exit(1);
        }

        var env = envOrError.Value;

        if (verbose)
        {
            Console.WriteLine("🔧 Verbose mode enabled");
            Console.WriteLine($"📄 Environment variables source: {(envFileLoaded ? ".env file" : "system environment")}");
            Console.WriteLine($"📍 Domain: {env.Domain}");
            Console.WriteLine($"🏠 Subdomain: {env.Subdomain}");
            Console.WriteLine($"🌐 IPv4 URL: {env.Ipv4Url}");
        }

        using var httpClient = new HttpClient();

        // Initialize health service
        var healthService = new HealthService();

        // Start health check API if port is specified
        if (env.healthPort > 0)
        {
            var healthTask = StartHealthCheckApi(healthService, env.healthPort, verbose);
            if (verbose)
            {
                Console.WriteLine($"🏥 Health check API started on port {env.healthPort}");
                Console.WriteLine($"   GET http://localhost:{env.healthPort}/health");
                Console.WriteLine($"   GET http://localhost:{env.healthPort}/health/live");
                Console.WriteLine($"   GET http://localhost:{env.healthPort}/health/ready");
                Console.WriteLine($"   GET http://localhost:{env.healthPort}/status");
            }
        }

        string publicIpv4 = string.Empty;

        while (true)
        {
            healthService.UpdateAttempt();
            Console.WriteLine($"\n=== {DateTime.Now} ===");
            Console.WriteLine("Starting DDNS update process...\n");


            DnsApi dnsApi = new DnsApi(httpClient, env.Token, verbose);

            await GetPublicIpv4(env.Ipv4Url, httpClient)
                .ThenAsync(ipv4 =>
                {
                    var task = Task.FromResult(ipv4);
                    Console.WriteLine($"✅ Fetched public IPv4 address: {ipv4}\n");
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
                .ThenAsync(ipv4 => dnsApi.GetZones()
                    .Then(zones => (ipv4, zones)))
                .Then(tuple => dnsApi.GetZoneIdByName(tuple.zones, env.Domain)
                    .Then(zoneId => (tuple.ipv4, zoneId)))
                .ThenAsync(tuple => dnsApi.GetRecordsForZone(tuple.zoneId)
                    .Then(records => (tuple.ipv4, tuple.zoneId, records)))
                .Then(tuple => dnsApi.GetRecordByName(tuple.records, env.Subdomain)
                    .Then(record => (tuple.ipv4, tuple.zoneId, record)))
                .ThenAsync(tuple =>
                        dnsApi.UpdateRecord(tuple.record.Id, tuple.zoneId, "A", tuple.record.Name, tuple.ipv4, env.ttl)
                    .Then(result => (tuple.ipv4, result)))
                .MatchFirst(
                    result =>
                    {
                        Console.WriteLine("✅ DNS record updated successfully");
                        healthService.UpdateSuccessful(publicIpv4);
                        return true;
                    },
                    error =>
                    {
                        if (error.Type == 0 && error.Code == "NoUpdateNeeded")
                        {
                            Console.WriteLine("ℹ️  " + error.Description);
                            healthService.UpdateSuccessful(publicIpv4); // Still successful, just no change needed
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"\nError: {error}");
                            Console.WriteLine("❌ Failed to update DNS record.");
                            healthService.UpdateFailed(error.Description);
                        }
                        return false;
                    }
                );

            Console.WriteLine($"\nℹ️  Waiting {env.interval} minutes before next check...");
            await Task.Delay(TimeSpan.FromMinutes(env.interval));
        }

    }

    record struct EnvInfo(string Ipv4Url, string Token, string Domain, string Subdomain, int ttl, int interval, int healthPort);

    private static bool AreRequiredEnvVarsPresent()
    {
        var requiredVars = new[] { "IPV4_URL", "TOKEN", "DOMAIN", "SUBDOMAIN" };
        return requiredVars.All(varName =>
        {
            var value = Environment.GetEnvironmentVariable(varName);
            return !string.IsNullOrEmpty(value);
        });
    }

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

        var ttlStr = Environment.GetEnvironmentVariable("TTL");
        int ttl = 7200; // Default TTL
        if (ttlStr is not null and not "")
        {
            if (!int.TryParse(ttlStr, out ttl))
            {
                Console.WriteLine("TTL environment variable is not a valid integer");
                return Error.Validation(description: "TTL environment variable is not a valid integer");
            }
        }

        var intervalStr = Environment.GetEnvironmentVariable("INTERVAL");
        int interval = 10; // Default interval in minutes
        if (intervalStr is not null and not "")
        {
            if (!int.TryParse(intervalStr, out interval))
            {
                Console.WriteLine("INTERVAL environment variable is not a valid integer");
                return Error.Validation(description: "INTERVAL environment variable is not a valid integer");
            }
        }

        var healthPortStr = Environment.GetEnvironmentVariable("HEALTH_PORT");
        int healthPort = 8080; // Default health port
        if (healthPortStr is not null and not "")
        {
            if (!int.TryParse(healthPortStr, out healthPort))
            {
                Console.WriteLine("HEALTH_PORT environment variable is not a valid integer");
                return Error.Validation(description: "HEALTH_PORT environment variable is not a valid integer");
            }
        }

        return new EnvInfo(ipv4Url, token, domain, subdomain, ttl, interval, healthPort);
    }

    public static async Task<ErrorOr<string>> GetPublicIpv4(string ipv4Url, HttpClient httpClient)
    {
        try
        {
            string ipv4 = await httpClient.GetStringAsync(ipv4Url);
            return ipv4.Trim(); // Trim whitespace including newlines
        }
        catch (Exception ex)
        {
            return Error.Unexpected(description: $"Failed to fetch IPv4: {ex.Message}");
        }
    }

    private static async Task StartHealthCheckApi(HealthService healthService, int port, bool verbose)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        if (verbose)
        {
            Console.WriteLine($"🏥 Health check server listening on port {port}");
        }

        while (true)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                string responseString;
                int statusCode = 200;

                switch (request.Url?.AbsolutePath)
                {
                    case "/health":
                        var healthStatus = healthService.GetStatus();
                        responseString = healthStatus.IsHealthy
                            ? """{"status":"healthy"}"""
                            : """{"status":"unhealthy"}""";
                        statusCode = healthStatus.IsHealthy ? 200 : 503;
                        break;

                    case "/health/live":
                        responseString = """{"status":"alive"}""";
                        break;

                    case "/health/ready":
                        var readyStatus = healthService.GetStatus();
                        responseString = readyStatus.IsHealthy
                            ? """{"status":"ready"}"""
                            : """{"status":"not ready"}""";
                        statusCode = readyStatus.IsHealthy ? 200 : 503;
                        break;

                    case "/status":
                        var detailedStatus = healthService.GetStatus();
                        responseString = $$"""
                        {
                            "healthy": {{detailedStatus.IsHealthy.ToString().ToLower()}},
                            "uptime": "{{detailedStatus.Uptime.TotalHours:F1}} hours",
                            "startTime": "{{detailedStatus.StartTime:O}}",
                            "lastSuccessfulUpdate": "{{detailedStatus.LastSuccessfulUpdate:O}}",
                            "lastUpdateAttempt": "{{detailedStatus.LastUpdateAttempt:O}}",
                            "timeSinceLastUpdateMinutes": {{detailedStatus.TimeSinceLastUpdateMinutes}},
                            "timeSinceLastAttemptMinutes": {{detailedStatus.TimeSinceLastAttemptMinutes}},
                            "currentIp": "{{detailedStatus.CurrentIp}}",
                            "lastError": "{{detailedStatus.LastError}}"
                        }
                        """;
                        break;

                    default:
                        responseString = """{"error":"Not found"}""";
                        statusCode = 404;
                        break;
                }

                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    Console.WriteLine($"Health check server error: {ex.Message}");
                }
            }
        }
    }
}
