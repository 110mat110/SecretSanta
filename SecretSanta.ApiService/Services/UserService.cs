using SecretSanta.ApiService.Database;
using SecretSanta.ApiService.Database.Model;
using Microsoft.EntityFrameworkCore;
using SecretSanta.ServiceDefaults.Models;

namespace SecretSanta.ApiService.Services;

public class UserService(Context context)
{
    public async Task<User?> GetUser(string googleId)
    {
        return await context.Users.FirstOrDefaultAsync(x => x.GoogleId == googleId);
    }

    internal async Task<Guid> EnsureCreation(ApiUser user)
    {
        if (string.IsNullOrWhiteSpace(user.GoogleId) || string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("Authenticated Google ID and email are required.");
        }

        var dbUser = await context.Users.FirstOrDefaultAsync(x => x.GoogleId == user.GoogleId);

        if (dbUser == null)
        {
            dbUser = new User()
            {
                Id = Guid.NewGuid(),
                Name = user.Name,
                Email = user.Email,
                GoogleId = user.GoogleId,
            };
            await context.Users.AddAsync(dbUser);
            await context.SaveChangesAsync();
        }
        else
        {
            dbUser.Name = user.Name ?? dbUser.Name;
            dbUser.Email = user.Email;
            context.Users.Update(dbUser);
            await context.SaveChangesAsync();

        }


        return dbUser.Id;
    }
}
