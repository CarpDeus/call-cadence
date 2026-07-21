using CallCadence.Domain.ApiCall;

namespace CallCadence.Application.ApiCall;

/// <summary>
/// Service for managing API call definitions with archiving support.
/// </summary>
public sealed class ApiCallManagementService
{
    private readonly IApiCallRepository _apiCallRepository;
    private readonly IApiCallArchiveRepository _archiveRepository;

    public ApiCallManagementService(
        IApiCallRepository apiCallRepository,
        IApiCallArchiveRepository archiveRepository)
    {
        _apiCallRepository = apiCallRepository;
        _archiveRepository = archiveRepository;
    }

    public async Task<ApiCallDto> CreateAsync(CreateApiCallDto dto)
    {
        ValidateNoMacrosInNames(dto.Headers, dto.Parameters);

        var apiCall = new Domain.ApiCall.ApiCall
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            HttpMethod = dto.HttpMethod,
            EndpointUrl = dto.EndpointUrl,
            Payload = dto.Payload,
            Headers = dto.Headers,
            Parameters = dto.Parameters,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        var created = await _apiCallRepository.CreateAsync(apiCall);
        return MapToDto(created);
    }

    public async Task<IEnumerable<ApiCallDto>> CreateManyAsync(IEnumerable<CreateApiCallDto> dtos)
    {
        var results = new List<ApiCallDto>();
        foreach (var dto in dtos)
        {
            var result = await CreateAsync(dto);
            results.Add(result);
        }
        return results;
    }

    public async Task<ApiCallDto> UpdateAsync(UpdateApiCallDto dto)
    {
        ValidateNoMacrosInNames(dto.Headers, dto.Parameters);

        var existing = await _apiCallRepository.GetByIdAsync(dto.Id);
        if (existing == null)
        {
            throw new InvalidOperationException($"API call with ID {dto.Id} not found");
        }

        // Archive the current version before updating
        var archive = new ApiCallArchive
        {
            Id = Guid.NewGuid(),
            ApiCallId = existing.Id,
            Title = existing.Title,
            Description = existing.Description,
            HttpMethod = existing.HttpMethod,
            EndpointUrl = existing.EndpointUrl,
            Payload = existing.Payload,
            Headers = existing.Headers,
            Parameters = existing.Parameters,
            IsActive = existing.IsActive,
            ArchivedAt = DateTime.UtcNow,
            OriginalCreatedAt = existing.CreatedAt,
            OriginalModifiedAt = existing.ModifiedAt
        };
        await _archiveRepository.CreateAsync(archive);

        // Update the API call
        existing.Title = dto.Title;
        existing.Description = dto.Description;
        existing.HttpMethod = dto.HttpMethod;
        existing.EndpointUrl = dto.EndpointUrl;
        existing.Payload = dto.Payload;
        existing.Headers = dto.Headers;
        existing.Parameters = dto.Parameters;
        existing.IsActive = dto.IsActive;
        existing.ModifiedAt = DateTime.UtcNow;

        var updated = await _apiCallRepository.UpdateAsync(existing);
        return MapToDto(updated);
    }

    public async Task<IEnumerable<ApiCallDto>> UpdateManyAsync(IEnumerable<UpdateApiCallDto> dtos)
    {
        var results = new List<ApiCallDto>();
        foreach (var dto in dtos)
        {
            var result = await UpdateAsync(dto);
            results.Add(result);
        }
        return results;
    }

    public async Task<ApiCallDto?> GetByIdAsync(Guid id)
    {
        var apiCall = await _apiCallRepository.GetByIdAsync(id);
        return apiCall == null ? null : MapToDto(apiCall);
    }

    public async Task<IEnumerable<ApiCallDto>> GetAllAsync()
    {
        var apiCalls = await _apiCallRepository.GetAllAsync();
        return apiCalls.Select(MapToDto);
    }

    public async Task<IEnumerable<ApiCallDto>> GetActiveAsync()
    {
        var apiCalls = await _apiCallRepository.GetActiveAsync();
        return apiCalls.Select(MapToDto);
    }

    public async Task ActivateAsync(Guid id)
    {
        var apiCall = await _apiCallRepository.GetByIdAsync(id);
        if (apiCall == null)
        {
            throw new InvalidOperationException($"API call with ID {id} not found");
        }

        if (!apiCall.IsActive)
        {
            apiCall.IsActive = true;
            apiCall.ModifiedAt = DateTime.UtcNow;
            await _apiCallRepository.UpdateAsync(apiCall);
        }
    }

    public async Task ActivateManyAsync(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            await ActivateAsync(id);
        }
    }

    public async Task DeactivateAsync(Guid id)
    {
        var apiCall = await _apiCallRepository.GetByIdAsync(id);
        if (apiCall == null)
        {
            throw new InvalidOperationException($"API call with ID {id} not found");
        }

        if (apiCall.IsActive)
        {
            apiCall.IsActive = false;
            apiCall.ModifiedAt = DateTime.UtcNow;
            await _apiCallRepository.UpdateAsync(apiCall);
        }
    }

    public async Task DeactivateManyAsync(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            await DeactivateAsync(id);
        }
    }

    private static ApiCallDto MapToDto(Domain.ApiCall.ApiCall apiCall)
    {
        return new ApiCallDto
        {
            Id = apiCall.Id,
            Title = apiCall.Title,
            Description = apiCall.Description,
            HttpMethod = apiCall.HttpMethod,
            EndpointUrl = apiCall.EndpointUrl,
            Payload = apiCall.Payload,
            Headers = apiCall.Headers,
            Parameters = apiCall.Parameters,
            IsActive = apiCall.IsActive,
            CreatedAt = apiCall.CreatedAt,
            ModifiedAt = apiCall.ModifiedAt
        };
    }

    private static void ValidateNoMacrosInNames(IEnumerable<NamedValue> headers, IEnumerable<NamedValue> parameters)
    {
        if (headers.Any(h => ContainsMacroIdentifier(h.Name)))
        {
            throw new ArgumentException("Header names cannot contain macro identifiers.");
        }

        if (parameters.Any(p => ContainsMacroIdentifier(p.Name)))
        {
            throw new ArgumentException("Parameter names cannot contain macro identifiers.");
        }
    }

    private static bool ContainsMacroIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var start = value.IndexOf("@@", StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var end = value.IndexOf("@@", start + 2, StringComparison.Ordinal);
        return end >= 0;
    }
}
