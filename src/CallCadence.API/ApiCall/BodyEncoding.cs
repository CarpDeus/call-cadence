using CallCadence.Domain.ApiCall;

namespace CallCadence.Infrastructure.ApiCall;

public sealed class BodyEncoding
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public static IReadOnlyList<BodyEncoding> SeedData =>
    [
        new() { Id = ApiBodyEncoding.Json, Name = ApiBodyEncoding.GetDisplayName(ApiBodyEncoding.Json) },
        new() { Id = ApiBodyEncoding.Xml, Name = ApiBodyEncoding.GetDisplayName(ApiBodyEncoding.Xml) },
        new() { Id = ApiBodyEncoding.FormUrlEncoded, Name = ApiBodyEncoding.GetDisplayName(ApiBodyEncoding.FormUrlEncoded) }
    ];
}
