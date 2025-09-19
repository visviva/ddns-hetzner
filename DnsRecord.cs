using System.Text.Json.Serialization;

namespace DdnsHetzner;

public record Record(
    [property: JsonPropertyName("type")]
    string Type,

    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("created"), JsonConverter(typeof(HetznerDateTimeConverter))]
    DateTime? Created,

    [property: JsonPropertyName("modified"), JsonConverter(typeof(HetznerDateTimeConverter))]
    DateTime? Modified,

    [property: JsonPropertyName("zone_id")]
    string ZoneId,

    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("value")]
    string Value,

    [property: JsonPropertyName("ttl")]
    int? Ttl
);

public record RecordList(
    [property: JsonPropertyName("records")]
    IReadOnlyList<Record> Records
);


public enum DnsRecordType
{
    A,
    AAAA,
    NS,
    MX,
    CNAME,
    RP,
    TXT,
    SOA,
    HINFO,
    SRV,
    DANE,
    TLSA,
    DS,
    CAA
}

public record DnsRecord(
    [property: JsonPropertyName("zone_id")]
    string ZoneId,

    [property: JsonPropertyName("type"), JsonConverter(typeof(JsonStringEnumConverter))]
    DnsRecordType Type,

    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("value")]
    string Value,

    [property: JsonPropertyName("ttl")]
    int? Ttl
);