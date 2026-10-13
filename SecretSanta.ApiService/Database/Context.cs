using SecretSanta.ApiService.Database.Model;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SecretSanta.ApiService.Database;

public class Context(DbContextOptions<Context> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Group> Groups { get; set; } = null!;
    public DbSet<Wish> Wishes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.GoogleId).IsUnique();
        modelBuilder.Entity<Wish>()
            .HasOne(bc => bc.Owner)
            .WithMany(bc => bc.Wishes)
            .HasForeignKey(bc => bc.OwnerId);
    }
    }
