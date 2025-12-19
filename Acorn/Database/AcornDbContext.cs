using Acorn.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acorn.Database;

public class AcornDbContext : DbContext
{
    private readonly DatabaseOptions _options;

    public AcornDbContext(DbContextOptions<AcornDbContext> options, IOptions<DatabaseOptions> dbOptions) 
        : base(options)
    {
        _options = dbOptions.Value;
    }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Character> Characters { get; set; }
    public DbSet<Guild> Guilds { get; set; }
    public DbSet<CharacterItem> CharacterItems { get; set; }
    public DbSet<CharacterPaperdoll> CharacterPaperdolls { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Account entity
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Username);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Password).IsRequired();
            entity.Property(e => e.Salt).IsRequired();
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Country).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Created).IsRequired();
            entity.Property(e => e.LastUsed).IsRequired();

            // Navigation property
            entity.HasMany(e => e.Characters)
                .WithOne()
                .HasForeignKey(c => c.Accounts_Username)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Character entity
        modelBuilder.Entity<Character>(entity =>
        {
            entity.HasKey(e => e.Name);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Accounts_Username).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Home).HasMaxLength(100);
            entity.Property(e => e.Fiance).HasMaxLength(16);
            entity.Property(e => e.Partner).HasMaxLength(16);
            entity.Property(e => e.Admin).IsRequired();
            entity.Property(e => e.Class).IsRequired();
            entity.Property(e => e.Gender).IsRequired();
            entity.Property(e => e.Race).IsRequired();
            
            // Configure relationships
            entity.HasMany(e => e.Items)
                .WithOne(i => i.Character)
                .HasForeignKey(i => i.CharacterName)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Paperdoll)
                .WithOne(p => p.Character)
                .HasForeignKey<CharacterPaperdoll>(p => p.CharacterName)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure CharacterItem entity
        modelBuilder.Entity<CharacterItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CharacterName).IsRequired().HasMaxLength(16);
            entity.Property(e => e.ItemId).IsRequired();
            entity.Property(e => e.Amount).IsRequired();
            entity.Property(e => e.Slot).IsRequired();
            
            // Create index for faster queries
            entity.HasIndex(e => new { e.CharacterName, e.Slot });
        });
        
        // Configure CharacterPaperdoll entity
        modelBuilder.Entity<CharacterPaperdoll>(entity =>
        {
            entity.HasKey(e => e.CharacterName);
            entity.Property(e => e.CharacterName).IsRequired().HasMaxLength(16);
        });

        // Configure Guild entity
        modelBuilder.Entity<Guild>(entity =>
        {
            entity.HasKey(e => e.Tag);
            entity.Property(e => e.Tag).IsRequired().HasMaxLength(3);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Ranks).HasMaxLength(500);
        });

        // Seed default account
        modelBuilder.Entity<Account>().HasData(
            new Account
            {
                Username = "acorn",
                Password = "1I+dieTmkT9qbF9YjSt1pkRvgAkAHqcStjRxOzuHwSc=",
                Salt = "acorn",
                FullName = "acorn",
                Location = "acorn",
                Email = "acorn@acorn-eo.dev",
                Country = "acorn",
                Created = new DateTime(2024, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                LastUsed = new DateTime(2024, 8, 31, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
