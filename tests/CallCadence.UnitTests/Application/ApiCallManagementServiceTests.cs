using CallCadence.Application.ApiCall;
using CallCadence.Domain.ApiCall;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace CallCadence.UnitTests.Application;

[TestFixture]
public sealed class ApiCallManagementServiceTests
{
    private Mock<IApiCallRepository> _mockApiCallRepository = null!;
    private Mock<IApiCallArchiveRepository> _mockArchiveRepository = null!;
    private ApiCallManagementService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mockApiCallRepository = new Mock<IApiCallRepository>();
        _mockArchiveRepository = new Mock<IApiCallArchiveRepository>();
        _service = new ApiCallManagementService(_mockApiCallRepository.Object, _mockArchiveRepository.Object);
    }

    [Test]
    public async Task CreateAsync_ShouldCreateApiCallWithGeneratedId()
    {
        // Arrange
        var dto = new CreateApiCallDto
        {
            Title = "Test API",
            Description = "Test Description",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com",
            IsActive = true
        };

        _mockApiCallRepository.Setup(r => r.CreateAsync(It.IsAny<ApiCall>()))
            .ReturnsAsync((ApiCall ac) => ac);

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be(dto.Title);
        result.Description.Should().Be(dto.Description);
        result.HttpMethod.Should().Be(dto.HttpMethod);
        result.EndpointUrl.Should().Be(dto.EndpointUrl);
        result.IsActive.Should().Be(dto.IsActive);
        
        _mockApiCallRepository.Verify(r => r.CreateAsync(It.IsAny<ApiCall>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_ShouldThrowArgumentException_WhenHeaderNameContainsMacroIdentifier()
    {
        // Arrange
        var dto = new CreateApiCallDto
        {
            Title = "Test API",
            Description = "Test Description",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com",
            Headers =
            [
                new NamedValue { Name = "@@yyyy@@", Value = "value" }
            ]
        };

        // Act & Assert
        var act = async () => await _service.CreateAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Header names cannot contain macro identifiers.");
    }

    [Test]
    public async Task UpdateAsync_ShouldArchiveOldVersionAndUpdateApiCall()
    {
        // Arrange
        var existingApiCall = new ApiCall
        {
            Id = Guid.NewGuid(),
            Title = "Old Title",
            Description = "Old Description",
            HttpMethod = "GET",
            EndpointUrl = "https://old.example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ModifiedAt = DateTime.UtcNow.AddDays(-1)
        };

        var updateDto = new UpdateApiCallDto
        {
            Id = existingApiCall.Id,
            Title = "New Title",
            Description = "New Description",
            HttpMethod = "POST",
            EndpointUrl = "https://new.example.com",
            IsActive = false
        };

        _mockApiCallRepository.Setup(r => r.GetByIdAsync(existingApiCall.Id))
            .ReturnsAsync(existingApiCall);
        _mockApiCallRepository.Setup(r => r.UpdateAsync(It.IsAny<ApiCall>()))
            .ReturnsAsync((ApiCall ac) => ac);
        _mockArchiveRepository.Setup(r => r.CreateAsync(It.IsAny<ApiCallArchive>()))
            .ReturnsAsync((ApiCallArchive a) => a);

        // Act
        var result = await _service.UpdateAsync(updateDto);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(updateDto.Title);
        result.Description.Should().Be(updateDto.Description);
        result.HttpMethod.Should().Be(updateDto.HttpMethod);
        result.EndpointUrl.Should().Be(updateDto.EndpointUrl);
        result.IsActive.Should().Be(updateDto.IsActive);
        
        _mockArchiveRepository.Verify(r => r.CreateAsync(It.IsAny<ApiCallArchive>()), Times.Once);
        _mockApiCallRepository.Verify(r => r.UpdateAsync(It.IsAny<ApiCall>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_ShouldThrowArgumentException_WhenParameterNameContainsMacroIdentifier()
    {
        // Arrange
        var updateDto = new UpdateApiCallDto
        {
            Id = Guid.NewGuid(),
            Title = "New Title",
            Parameters =
            [
                new NamedValue { Name = "@@MM@@", Value = "value" }
            ]
        };

        // Act & Assert
        var act = async () => await _service.UpdateAsync(updateDto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Parameter names cannot contain macro identifiers.");
    }

    [Test]
    public async Task UpdateAsync_ShouldThrowException_WhenApiCallNotFound()
    {
        // Arrange
        var updateDto = new UpdateApiCallDto
        {
            Id = Guid.NewGuid(),
            Title = "New Title"
        };

        _mockApiCallRepository.Setup(r => r.GetByIdAsync(updateDto.Id))
            .ReturnsAsync((ApiCall?)null);

        // Act & Assert
        var act = async () => await _service.UpdateAsync(updateDto);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"API call with ID {updateDto.Id} not found");
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnApiCall_WhenExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var apiCall = new ApiCall
        {
            Id = id,
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com"
        };

        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(apiCall);

        // Act
        var result = await _service.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Title.Should().Be(apiCall.Title);
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync((ApiCall?)null);

        // Act
        var result = await _service.GetByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task ActivateAsync_ShouldActivateApiCall_WhenInactive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var apiCall = new ApiCall
        {
            Id = id,
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com",
            IsActive = false
        };

        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(apiCall);
        _mockApiCallRepository.Setup(r => r.UpdateAsync(It.IsAny<ApiCall>()))
            .ReturnsAsync((ApiCall ac) => ac);

        // Act
        await _service.ActivateAsync(id);

        // Assert
        apiCall.IsActive.Should().BeTrue();
        _mockApiCallRepository.Verify(r => r.UpdateAsync(It.Is<ApiCall>(a => a.IsActive)), Times.Once);
    }

    [Test]
    public async Task ActivateAsync_ShouldNotUpdateDatabase_WhenAlreadyActive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var apiCall = new ApiCall
        {
            Id = id,
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com",
            IsActive = true
        };

        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(apiCall);

        // Act
        await _service.ActivateAsync(id);

        // Assert
        _mockApiCallRepository.Verify(r => r.UpdateAsync(It.IsAny<ApiCall>()), Times.Never);
    }

    [Test]
    public async Task ActivateAsync_ShouldThrowException_WhenApiCallNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync((ApiCall?)null);

        // Act & Assert
        var act = async () => await _service.ActivateAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"API call with ID {id} not found");
    }

    [Test]
    public async Task DeactivateAsync_ShouldDeactivateApiCall_WhenActive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var apiCall = new ApiCall
        {
            Id = id,
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com",
            IsActive = true
        };

        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(apiCall);
        _mockApiCallRepository.Setup(r => r.UpdateAsync(It.IsAny<ApiCall>()))
            .ReturnsAsync((ApiCall ac) => ac);

        // Act
        await _service.DeactivateAsync(id);

        // Assert
        apiCall.IsActive.Should().BeFalse();
        _mockApiCallRepository.Verify(r => r.UpdateAsync(It.Is<ApiCall>(a => !a.IsActive)), Times.Once);
    }

    [Test]
    public async Task DeactivateAsync_ShouldNotUpdateDatabase_WhenAlreadyInactive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var apiCall = new ApiCall
        {
            Id = id,
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com",
            IsActive = false
        };

        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(apiCall);

        // Act
        await _service.DeactivateAsync(id);

        // Assert
        _mockApiCallRepository.Verify(r => r.UpdateAsync(It.IsAny<ApiCall>()), Times.Never);
    }

    [Test]
    public async Task DeactivateAsync_ShouldThrowException_WhenApiCallNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockApiCallRepository.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync((ApiCall?)null);

        // Act & Assert
        var act = async () => await _service.DeactivateAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"API call with ID {id} not found");
    }
}
