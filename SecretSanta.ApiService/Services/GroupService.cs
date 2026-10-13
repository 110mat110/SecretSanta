using Microsoft.EntityFrameworkCore;
using SecretSanta.ApiService.Database;
using SecretSanta.ApiService.Database.Model;
using SecretSanta.ServiceDefaults.Models;

namespace SecretSanta.ApiService.Services;

public class GroupService(Context context)
{
    public async Task<List<ApiGroup>> GroupsByGoogleIdAsync(string googleId)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.GoogleId == googleId);

        return user is null ? [] : await context.Groups.Where(x => x.OwnerId == user.Id).Select(x => new ApiGroup()
        {
            Id = x.Id,
            Deadline = x.Deadline,
            IsActive = x.IsActive,
            OwnerId = x.OwnerId,
            WishesPerPerson = x.WishesPerPerson,
            Description = x.Description,
            Name = x.Name,
        }).ToListAsync();
    }

    public async Task<bool> CloseGroupAsync(Guid groupId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var group = await context.Groups.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == groupId);
        if (group is null || !group.IsActive) return false;
        if (group.Deadline > DateTime.UtcNow) return false;

        var wishes = await context.Wishes.Where(x => x.GroupId == groupId).ToListAsync();
        var wishesByOwner = wishes.GroupBy(x => x.OwnerId).ToDictionary(x => x.Key, x => x.ToList());
        if (wishesByOwner.Count < 2
            || wishesByOwner.Any(x => x.Value.Count != group.WishesPerPerson)
            || group.WishesPerPerson >= wishesByOwner.Count)
        {
            return false;
        }

        var participants = wishesByOwner.Keys.ToArray();
        Random.Shared.Shuffle(participants);

        for (var round = 0; round < group.WishesPerPerson; round++)
        {
            var offset = round + 1;
            for (var i = 0; i < participants.Length; i++)
            {
                var ownerId = participants[i];
                var targetId = participants[(i + offset) % participants.Length];
                wishesByOwner[ownerId][round].TargetId = targetId;
            }
        }

        group.IsActive = false;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<ApiGroup?> CreateGroup(string googleId)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.GoogleId == googleId);

        if (user is null) { return null; }

        var group = new Group()
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            Deadline = DateTime.UtcNow.AddDays(7),
            Description = "",
            Name = "Vianočná skupina",
            OwnerId = user.Id,
            WishesPerPerson = 1,
        };

        await context.Groups.AddAsync(group);
        await context.SaveChangesAsync();

        return new ApiGroup()
        {
            Id = group.Id,
            Deadline = group.Deadline,
            IsActive = group.IsActive,
            OwnerId = group.OwnerId,
            WishesPerPerson = group.WishesPerPerson,
            Description = group.Description,
            Name = group.Name,
        };
    }

    internal async Task<ApiGroup?> GetGroup(Guid groupId)
    {
        var group = await context.Groups.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == groupId);

        return group is null
            ? null
            : new ApiGroup()
        {
            Id = group.Id,
            IsActive = group.IsActive,
            Deadline = group.Deadline,
            Description = group.Description,
            Name = group.Name,
            OwnerId = group.OwnerId,
            WishesPerPerson = group.WishesPerPerson
        };
    }

    internal async Task<ApiGroup?> EditGroup(Guid groupId, ApiGroup apiGroup, string googleId)
    {
        var group = await context.Groups.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == groupId);

        if (group is null || !group.IsActive || group.Owner.GoogleId != googleId) return null;
        if (apiGroup.WishesPerPerson is < 1 or > 20
            || apiGroup.Deadline <= DateTime.UtcNow
            || string.IsNullOrWhiteSpace(apiGroup.Name)
            || apiGroup.Name.Length > 100
            || string.IsNullOrWhiteSpace(apiGroup.Description)
            || apiGroup.Description.Length > 2000)
        {
            return null;
        }

        group.Deadline = apiGroup.Deadline.ToUniversalTime();
        group.Name = apiGroup.Name;
        group.Description = apiGroup.Description;
        group.WishesPerPerson = apiGroup.WishesPerPerson;

        context.Update(group);
        await context.SaveChangesAsync();

        return new ApiGroup()
        {
            Id = group.Id,
            IsActive = group.IsActive,
            Deadline = group.Deadline,
            Description = group.Description,
            Name = group.Name,
            OwnerId = group.OwnerId,
            WishesPerPerson = group.WishesPerPerson
        };

    }

    internal async Task<bool> CloseGroupAsync(Guid groupId, string googleId)
    {
        var group = await context.Groups.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == groupId);
        if (group is null || group.Owner.GoogleId != googleId)
        {
            return false;
        }

        return await CloseGroupAsync(groupId);
    }
}
