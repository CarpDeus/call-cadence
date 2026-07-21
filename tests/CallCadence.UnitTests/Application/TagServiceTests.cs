using CallCadence.Application.Tags;
using CallCadence.Domain.Tags;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace CallCadence.UnitTests.Application;

[TestFixture]
public sealed class TagServiceTests
{
    private Mock<ITagRepository> _tagRepository = null!;
    private TagService _service = null!;

    [SetUp]
    public void Setup()
    {
        _tagRepository = new Mock<ITagRepository>();
        _service = new TagService(_tagRepository.Object);
    }

    [Test]
    public async Task AddAsync_ShouldNormalizeAndPersistTag()
    {
        _tagRepository.Setup(repository => repository.GetByValueAsync("#hello_world"))
            .ReturnsAsync((Tag?)null);
        _tagRepository.Setup(repository => repository.CreateAsync(It.IsAny<Tag>()))
            .ReturnsAsync((Tag tag) => tag);

        var result = await _service.AddAsync(" #Hello   World ");

        result.Value.Should().Be("#hello_world");
        _tagRepository.Verify(repository => repository.GetByValueAsync("#hello_world"), Times.Once);
        _tagRepository.Verify(repository => repository.CreateAsync(It.Is<Tag>(tag => tag.Value == "#hello_world")), Times.Once);
    }

    [Test]
    public async Task AddAsync_ShouldReturnExistingTag_WhenNormalizedTagAlreadyExists()
    {
        _tagRepository.Setup(repository => repository.GetByValueAsync("#hello_world"))
            .ReturnsAsync(new Tag { Value = "#hello_world" });

        var result = await _service.AddAsync("#HELLO world");

        result.Value.Should().Be("#hello_world");
        _tagRepository.Verify(repository => repository.CreateAsync(It.IsAny<Tag>()), Times.Never);
    }

    [Test]
    public async Task LookupAsync_ShouldNormalizePartialValue()
    {
        _tagRepository.Setup(repository => repository.LookupAsync("hello_world"))
            .ReturnsAsync(
            [
                new Tag { Value = "#hello_world" },
                new Tag { Value = "#say_hello_world" }
            ]);

        var result = await _service.LookupAsync("Hello   World");

        result.Select(tag => tag.Value).Should().Equal("#hello_world", "#say_hello_world");
        _tagRepository.Verify(repository => repository.LookupAsync("hello_world"), Times.Once);
    }

    [Test]
    public async Task AddAsync_ShouldThrow_WhenTagIsBlank()
    {
        var act = async () => await _service.AddAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Tag must contain at least one non-space character.*");
    }
}
