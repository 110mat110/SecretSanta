using Microsoft.AspNetCore.Mvc;
using SecretSanta.ApiService.Services;
using SecretSanta.ServiceDefaults.Models;

namespace SecretSanta.ApiService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GroupController(GroupService groupService) : ControllerBase
{
    [HttpGet("all/{nid}")]
    public async Task<List<ApiGroup>> Groups(string nid)
    {
        return await groupService.GroupsByGoogleIdAsync(nid);
    }
    [HttpGet("{groupId}")]
    public async Task<IActionResult> Group(Guid groupId)
    {
        var group = await groupService.GetGroup(groupId);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpPost("{nid}")]
    public async Task<ApiGroup?> CreateGroup(string nid)
    {
        return await groupService.CreateGroup(nid);

    }

    [HttpPut("{groupId}")]
    public async Task<ApiGroup?> EditGroup(Guid groupId, ApiGroup apiGroup, [FromQuery] string googleId)
    {
        return await groupService.EditGroup(groupId, apiGroup, googleId);

    }

    [HttpPost("{groupId}/close")]
    public async Task<IActionResult> CloseGroup(Guid groupId, [FromQuery] string googleId)
    {
        return await groupService.CloseGroupAsync(groupId, googleId) ? NoContent() : BadRequest();
    }
}
