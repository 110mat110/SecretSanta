using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecretSanta.ApiService.Services;
using SecretSanta.ServiceDefaults.Models;
using System.Threading.Tasks;

namespace SecretSanta.ApiService.Controllers;
[Route("api/[controller]")]
[ApiController]
public class WishesController(WishService wishService) : ControllerBase
{
    [HttpGet("{groupId}/{nid}")]
    public async Task<List<ApiWish>> GetMyWishesAsync(Guid groupId, string nid)
    {
        return await wishService.GetMyWishes(groupId, nid);
    }

    [HttpGet("{groupId}/{googleId}/assignments")]
    public async Task<List<ApiWish>> GetMyAssignments(Guid groupId, string googleId)
    {
        return await wishService.GetMyAssignments(groupId, googleId);
    }

    [HttpDelete("{wishId}")]
    public async Task<bool> RemoveWish(Guid wishId, [FromQuery] string googleId)
    {
        return await wishService.RemoveWishAsync(wishId, googleId);
    }

    [HttpPost("")]
    public async Task<ActionResult<ApiWish>> CreateWish(ApiWish wish, [FromQuery] string googleId)
    {
        var createdWish = await wishService.CreateWish(wish, googleId);
        return createdWish is null ? BadRequest() : Ok(createdWish);
    }
}
