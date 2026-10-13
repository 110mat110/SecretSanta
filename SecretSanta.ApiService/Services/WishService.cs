using Microsoft.EntityFrameworkCore;
using SecretSanta.ApiService.Database;
using SecretSanta.ServiceDefaults.Models;

namespace SecretSanta.ApiService.Services;

public class WishService(Context context)
{
    internal async Task<ApiWish?> CreateWish(ApiWish wish, string googleId)
    {
        var group = await context.Groups.FirstOrDefaultAsync(x => x.Id == wish.GroupId);
        var user = await context.Users.FirstOrDefaultAsync(x => x.GoogleId == googleId);
        if (group is null || !group.IsActive || user is null || string.IsNullOrWhiteSpace(wish.Text)
            || wish.Text.Length > 500)
        {
            return null;
        }

        var wishesCount = await context.Wishes.CountAsync(x => x.GroupId == group.Id && x.OwnerId == user.Id);
        if (wishesCount >= group.WishesPerPerson)
        {
            return null;
        }

        var dbWish = new Database.Model.Wish()
        {
            Id = Guid.NewGuid(),
            GroupId = wish.GroupId,
            OwnerId = user.Id,
            TargetId = null,
            Text = wish.Text,
        };

        await context.Wishes.AddAsync(dbWish);
        await context.SaveChangesAsync();

        return new ApiWish()
        {
            GroupId = dbWish.GroupId,
            Id = dbWish.Id,
            OwnerId = dbWish.OwnerId,
            Text = dbWish.Text,
            TargetId = dbWish.TargetId
        };
    }

    internal async Task<List<ApiWish>> GetMyWishes(Guid groupId, string googleId)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.GoogleId == googleId);

        return user == null
            ? []
            : await context.Wishes.Where(x => x.GroupId == groupId && x.OwnerId == user.Id).Select(x => new ApiWish()
        {
            Id = x.Id,
            GroupId = x.GroupId,
            OwnerId = x.OwnerId,
            Text = x.Text,
        }).ToListAsync();
    }

    internal async Task<List<ApiWish>> GetMyAssignments(Guid groupId, string googleId)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.GoogleId == googleId);
        if (user is null) return [];

        return await context.Wishes
            .Where(x => x.GroupId == groupId && x.TargetId == user.Id && !x.Group.IsActive)
            .Select(x => new ApiWish
            {
                Id = x.Id,
                GroupId = x.GroupId,
                OwnerId = x.OwnerId,
                Text = x.Text,
                OwnerName = x.Owner.Name
            })
            .ToListAsync();
    }

    internal async Task<bool> RemoveWishAsync(Guid wishId, string googleId)
    {
        var wishToRemove = await context.Wishes.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == wishId);

        if (wishToRemove is null || wishToRemove.Owner.GoogleId != googleId) { return false; }

        var group = await context.Groups.FirstOrDefaultAsync(x => x.Id == wishToRemove.GroupId);
        if (group is null || !group.IsActive) return false;

        context.Remove(wishToRemove);
        await context.SaveChangesAsync();
        return true;
    }
}
