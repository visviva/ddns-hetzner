using System;
using System.Net.Http;
using System.Threading.Tasks;
class Program
{
    static async Task Main(string[] args)
    {
        // Load environment variable
        string ipv4Url = Environment.GetEnvironmentVariable("IPV4_URL") ?? "https://ipv4.icanhazip.com";
        using var httpClient = new HttpClient();
        try
        {
            string ipv4 = await httpClient.GetStringAsync(ipv4Url);
            Console.WriteLine($"Your public IPv4 address: {ipv4.Trim()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch IPv4: {ex.Message}");
        }
    }
}
