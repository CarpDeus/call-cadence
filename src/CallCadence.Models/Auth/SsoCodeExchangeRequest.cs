namespace CallCadence.Models.Auth;

public sealed class SsoCodeExchangeRequest
{
    public string Code { get; set; } = string.Empty;
}
