using CallCadence.Models.Auth;
using CallCadence.Infrastructure.ApiCall;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.API.Auth;

public sealed class SsoConfigurationService : ISsoConfigurationService
{
    private readonly CallCadenceDbContext? _dbContext;
    private readonly IReadOnlyList<SsoConfiguration>? _envConfigs;

    /// <summary>
    /// Initialises the service. When <paramref name="envConfigs"/> is non-null the service
    /// operates in read-only mode sourced from the environment variable.
    /// </summary>
    public SsoConfigurationService(CallCadenceDbContext? dbContext = null,
        IReadOnlyList<SsoConfiguration>? envConfigs = null)
    {
        _dbContext = dbContext;
        _envConfigs = envConfigs;
    }

    /// <inheritdoc />
    public bool IsOverriddenByEnvironment => _envConfigs is not null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SsoConfigurationResponse>> GetAllAsync()
    {
        if (_envConfigs is not null)
            return _envConfigs.Select(Map).ToList().AsReadOnly();

        var configs = await _dbContext!.SsoConfigurations.OrderBy(c => c.Name).ToListAsync();
        return configs.Select(Map).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<SsoConfigurationResponse?> GetBySchemeNameAsync(string schemeName)
    {
        if (_envConfigs is not null)
        {
            var match = _envConfigs.FirstOrDefault(c =>
                string.Equals(c.SchemeName, schemeName, StringComparison.OrdinalIgnoreCase));
            return match is null ? null : Map(match);
        }

        var config = await _dbContext!.SsoConfigurations
            .FirstOrDefaultAsync(c => c.SchemeName == schemeName);
        return config is null ? null : Map(config);
    }

    /// <inheritdoc />
    public async Task<SsoConfigurationResponse> UpsertAsync(UpsertSsoConfigurationRequest request)
    {
        ThrowIfEnvironmentOverride();

        var schemeName = EnvVarSsoConfigurationProvider.DeriveSchemeNameFromProviderName(request.Name);

        var existing = await _dbContext!.SsoConfigurations
            .FirstOrDefaultAsync(c => c.SchemeName == schemeName);

        if (existing is null)
        {
            existing = new SsoConfiguration { SchemeName = schemeName };
            _dbContext.SsoConfigurations.Add(existing);
        }

        existing.Name = Normalize(request.Name) ?? string.Empty;
        existing.IsEnabled = request.IsEnabled;
        existing.Authority = Normalize(request.Authority);
        existing.ClientId = Normalize(request.ClientId);
        if (request.ClientSecret is not null)
        {
            existing.ClientSecret = Normalize(request.ClientSecret);
        }
        existing.MetadataAddress = Normalize(request.MetadataAddress);
        existing.CallbackPath = Normalize(request.CallbackPath);
        existing.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Map(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string schemeName)
    {
        ThrowIfEnvironmentOverride();

        var existing = await _dbContext!.SsoConfigurations
            .FirstOrDefaultAsync(c => c.SchemeName == schemeName);

        if (existing is not null)
        {
            _dbContext.SsoConfigurations.Remove(existing);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync()
    {
        if (_envConfigs is not null)
            return _envConfigs.Any(c => c.IsEnabled);

        return await _dbContext!.SsoConfigurations.AnyAsync(c => c.IsEnabled);
    }

    private void ThrowIfEnvironmentOverride()
    {
        if (_envConfigs is not null)
            throw new InvalidOperationException(
                "SSO configuration is managed via environment variable and cannot be modified through the UI.");
    }

    private static SsoConfigurationResponse Map(SsoConfiguration config)
    {
        return new SsoConfigurationResponse
        {
            Name = config.Name,
            SchemeName = config.SchemeName,
            IsEnabled = config.IsEnabled,
            Authority = config.Authority,
            ClientId = config.ClientId,
            ClientSecret = null,
            HasClientSecret = !string.IsNullOrWhiteSpace(config.ClientSecret),
            MetadataAddress = config.MetadataAddress,
            CallbackPath = config.CallbackPath,
            UpdatedAt = config.UpdatedAt == default ? null : config.UpdatedAt
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
