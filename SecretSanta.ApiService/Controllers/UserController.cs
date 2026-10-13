using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecretSanta.ApiService.Database.Model;
using SecretSanta.ApiService.Services;
using SecretSanta.ServiceDefaults.Models;

namespace SecretSanta.ApiService.Controllers;
[Route("api/[controller]")]
[ApiController]
public class UserController(UserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUserId(string googleId) {
        var user = await userService.GetUser(googleId);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<Guid> EnsureUserCreation(ApiUser user)
    {
        return await userService.EnsureCreation(user);
    }
}
