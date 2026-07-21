using CallCadence.Domain.ApiCall;
using FluentAssertions;
using NUnit.Framework;

namespace CallCadence.UnitTests.Domain;

[TestFixture]
public sealed class ApiCallTests
{
    [Test]
    public void ApiCall_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var apiCall = new ApiCall();

        // Assert
        apiCall.Id.Should().BeEmpty();
        apiCall.Title.Should().BeEmpty();
        apiCall.Description.Should().BeEmpty();
        apiCall.HttpMethod.Should().Be("GET");
        apiCall.EndpointUrl.Should().BeEmpty();
        apiCall.IsActive.Should().BeFalse();
    }

    [Test]
    public void ApiCall_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var modifiedAt = DateTime.UtcNow;
        var headers = new List<NamedValue> { new NamedValue { Name = "Authorization", Value = "Bearer token123" } };
        var parameters = new List<NamedValue> { new NamedValue { Name = "page", Value = "1" } };

        // Act
        var apiCall = new ApiCall
        {
            Id = id,
            Title = "Test API Call",
            Description = "Test Description",
            HttpMethod = "POST",
            EndpointUrl = "https://api.example.com/test",
            Payload = "{\"test\": \"data\"}",
            Headers = headers,
            Parameters = parameters,
            IsActive = true,
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt
        };

        // Assert
        apiCall.Id.Should().Be(id);
        apiCall.Title.Should().Be("Test API Call");
        apiCall.Description.Should().Be("Test Description");
        apiCall.HttpMethod.Should().Be("POST");
        apiCall.EndpointUrl.Should().Be("https://api.example.com/test");
        apiCall.Payload.Should().Be("{\"test\": \"data\"}");
        apiCall.Headers.Should().BeEquivalentTo(headers);
        apiCall.Parameters.Should().BeEquivalentTo(parameters);
        apiCall.IsActive.Should().BeTrue();
        apiCall.CreatedAt.Should().Be(createdAt);
        apiCall.ModifiedAt.Should().Be(modifiedAt);
    }
}
