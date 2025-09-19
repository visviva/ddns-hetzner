using System;
using System.Text.Json.Serialization;

namespace DdnsHetzner;

public record ZoneList(
    [property: JsonPropertyName("zones")]
    IReadOnlyList<Zone> Zones,

    [property: JsonPropertyName("meta")]
    Meta Meta
);

public record Meta(
    [property: JsonPropertyName("pagination")]
    Pagination Pagination
);

public record Pagination(
    [property: JsonPropertyName("page")]
    int? Page,

    [property: JsonPropertyName("per_page")]
    int? PerPage,

    [property: JsonPropertyName("last_page")]
    int? LastPage,

    [property: JsonPropertyName("total_entries")]
    int? TotalEntries
);

public record TxtVerification(
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("token")]
    string Token
);

public record Zone(
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("created"), JsonConverter(typeof(HetznerDateTimeConverter))]
    DateTime? Created,

    [property: JsonPropertyName("modified"), JsonConverter(typeof(HetznerDateTimeConverter))]
    DateTime? Modified,

    [property: JsonPropertyName("legacy_dns_host")]
    string LegacyDnsHost,

    [property: JsonPropertyName("legacy_ns")]
    IReadOnlyList<string> LegacyNs,

    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("ns")]
    IReadOnlyList<string> Ns,

    [property: JsonPropertyName("owner")]
    string Owner,

    [property: JsonPropertyName("paused")]
    bool? Paused,

    [property: JsonPropertyName("permission")]
    string Permission,

    [property: JsonPropertyName("project")]
    string Project,

    [property: JsonPropertyName("registrar")]
    string Registrar,

    [property: JsonPropertyName("status")]
    string Status,

    [property: JsonPropertyName("ttl")]
    int? Ttl,

    [property: JsonPropertyName("verified"), JsonConverter(typeof(HetznerDateTimeConverter))]
    DateTime? Verified,

    [property: JsonPropertyName("records_count")]
    int? RecordsCount,

    [property: JsonPropertyName("is_secondary_dns")]
    bool? IsSecondaryDns,

    [property: JsonPropertyName("txt_verification")]
    TxtVerification TxtVerification
);

