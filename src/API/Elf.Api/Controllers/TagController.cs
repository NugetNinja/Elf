﻿using Elf.Api.Data;
using Elf.Api.Features;

namespace Elf.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TagController : ControllerBase
{
    private readonly IMediator _mediator;

    public TagController(IMediator mediator) => _mediator = mediator;

    [HttpGet("list")]
    [ProducesResponseType(typeof(List<TagEntity>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var list = await _mediator.Send(new GetTagsQuery());
        return Ok(list);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateTagCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateTagRequest request)
    {
        var code = await _mediator.Send(new UpdateTagCommand(id, request));
        if (code == -1) return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var code = await _mediator.Send(new DeleteTagCommand(id));
        if (code == -1) return NotFound();

        return NoContent();
    }
}