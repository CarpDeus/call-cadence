namespace CallCadence.Domain.ApiCall;

public static class ApiBodyEncoding
{
    public const int Json = 1;
    public const int Xml = 2;
    public const int FormUrlEncoded = 3;

    public static readonly IReadOnlyList<BodyEncodingOption> All =
    [
        new(Json, "JSON"),
        new(Xml, "XML"),
        new(FormUrlEncoded, "x-www-form-urlencoded")
    ];

    public static string GetDisplayName(int value)
    {
        return value switch
        {
            Json => "JSON",
            Xml => "XML",
            FormUrlEncoded => "x-www-form-urlencoded",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported body encoding.")
        };
    }

    public static bool IsSupported(int? value)
    {
        return value is null
            || value == Json
            || value == Xml
            || value == FormUrlEncoded;
    }

    public static string GetContentType(int value)
    {
        return value switch
        {
            Json => "application/json",
            Xml => "application/xml",
            FormUrlEncoded => "application/x-www-form-urlencoded",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported body encoding.")
        };
    }
}
