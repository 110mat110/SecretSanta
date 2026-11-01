using Microsoft.EntityFrameworkCore;
using SecretSanta.ApiService.Database;
using SecretSanta.ApiService.Database.Model;
using SecretSanta.ServiceDefaults.Models;

namespace SecretSanta.ApiService.Services;

public class GroupService(Context context, EmailService emailService)
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
        return await TryCloseGroupAsync(groupId) is null;
    }

    /// <summary>Closes every active group whose deadline has passed (called by the external cron job).</summary>
    public async Task<CloseDueResult> CloseDueGroupsAsync()
    {
        var now = DateTime.UtcNow;
        var dueGroups = await context.Groups
            .Where(x => x.IsActive && x.Deadline <= now)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        var result = new CloseDueResult();
        foreach (var group in dueGroups)
        {
            // Fresh state for each group, so one failure doesn't affect the next
            context.ChangeTracker.Clear();
            string? error;
            try
            {
                error = await TryCloseGroupAsync(group.Id);
            }
            catch (Exception e) when (e is DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException)
            {
                // e.g. a concurrent manual close (serialization failure) – the next run will retry
                error = $"error: {e.GetBaseException().Message}";
            }
            if (error is null)
            {
                result.Closed.Add(new CloseDueItem(group.Id, group.Name, null));
            }
            else
            {
                // Stays open – the owner can fix it (e.g. missing wishes) and close it manually
                result.NotClosed.Add(new CloseDueItem(group.Id, group.Name, error));
            }
        }

        return result;
    }

    /// <summary>Draws the gifts and closes the group. Returns null on success, otherwise the reason it couldn't be closed.</summary>
    private async Task<string?> TryCloseGroupAsync(Guid groupId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var group = await context.Groups.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == groupId);
        if (group is null) return "not found";
        if (!group.IsActive) return "already closed";
        if (group.Deadline > DateTime.UtcNow) return "deadline not reached";

        var wishes = await context.Wishes.Where(x => x.GroupId == groupId).ToListAsync();
        var wishesByOwner = wishes.GroupBy(x => x.OwnerId).ToDictionary(x => x.Key, x => x.ToList());
        if (wishesByOwner.Count < 2)
        {
            return "fewer than 2 participants";
        }
        if (wishesByOwner.Any(x => x.Value.Count != group.WishesPerPerson))
        {
            return "some participants have not added all wishes";
        }
        if (group.WishesPerPerson >= wishesByOwner.Count)
        {
            return "wishes per person must be lower than the number of participants";
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

        await SendAssignmentEmailsAsync(group, wishes);
        return null;
    }

    /// <summary>Emails every participant the wishes they should buy (TargetId = the person buying the gift).</summary>
    private async Task SendAssignmentEmailsAsync(Group group, List<Wish> wishes)
    {
        var userIds = wishes.Select(x => x.OwnerId).Distinct().ToList();
        var users = await context.Users.Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var emails = wishes
            .Where(x => x.TargetId is not null && users.ContainsKey(x.TargetId.Value))
            .GroupBy(x => x.TargetId!.Value)
            .Select(buyer => new AssignmentEmail(
                users[buyer.Key].Email,
                users[buyer.Key].Name,
                group.Id,
                group.Name,
                buyer.Select(w => new GiftAssignment(users.TryGetValue(w.OwnerId, out var owner) ? owner.Name : "?", w.Text)).ToList()))
            .Where(x => !string.IsNullOrWhiteSpace(x.ToEmail))
            .ToList();

        // The group is already closed – email problems are only logged, they must not fail the request
        await emailService.SendAssignmentsAsync(emails);
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

    /// <summary>Deletes the group with all its wishes. Only the owner can do it.</summary>
    internal async Task<bool> DeleteGroupAsync(Guid groupId, string googleId)
    {
        var group = await context.Groups.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == groupId);
        if (group is null || string.IsNullOrEmpty(googleId) || group.Owner.GoogleId != googleId) return false;

        // Wishes are removed by the database (cascade delete on Wishes.GroupId)
        context.Groups.Remove(group);
        await context.SaveChangesAsync();
        return true;
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

public record CloseDueItem(Guid Id, string Name, string? Reason);

public class CloseDueResult
{
    public List<CloseDueItem> Closed { get; } = [];
    public List<CloseDueItem> NotClosed { get; } = [];
}
