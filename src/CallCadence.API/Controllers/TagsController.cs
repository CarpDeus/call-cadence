using CallCadence.Application.Tags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CallCadence.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TagsController : ControllerBase
{
    private readonly TagService _tagService;

    public TagsController(TagService tagService)
    {
        _tagService = tagService;
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> Add([FromBody] CreateTagDto dto)
    {
        try
        {
            var tag = await _tagService.AddAsync(dto.Value);
            return Ok(tag);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> Lookup([FromQuery] string? query)
    {
        var tags = await _tagService.LookupAsync(query);
        return Ok(tags);
    }
}
