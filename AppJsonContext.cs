using System.Text.Json.Serialization;

namespace DdnsHetzner;

[JsonSerializable(typeof(RecordList))]
[JsonSerializable(typeof(Record))]
[JsonSerializable(typeof(DnsRecord))]
[JsonSerializable(typeof(ZoneList))]
[JsonSerializable(typeof(Zone))]
[JsonSerializable(typeof(Meta))]
[JsonSerializable(typeof(Pagination))]
[JsonSerializable(typeof(DnsRecordType))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public partial class AppJsonContext : JsonSerializerContext
{
}