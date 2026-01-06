using Acorn.Database.Models;
using Microsoft.EntityFrameworkCore;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Database;

public class AcornDbContext : DbContext
{

    public AcornDbContext(DbContextOptions<AcornDbContext> options)
        : base(options)
    {
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
            entity.Property(e => e.Admin).IsRequired()
                .HasConversion(
                    v => (int)v,
                    v => (AdminLevel)v
                );
            entity.Property(e => e.Class).IsRequired();
            entity.Property(e => e.Gender).IsRequired()
                .HasConversion(
                    v => (int)v,
                    v => (Gender)v
                )
                .HasColumnType("INTEGER");
            entity.Property(e => e.Race).IsRequired();
            entity.Property(e => e.Direction)
                .HasConversion(
                    v => (int)v,
                    v => (Direction)v
                );

            // Configure stat properties - "Int" is a reserved keyword in SQLite, use quoted name
            entity.Property(e => e.Str).IsRequired();
            entity.Property(e => e.Int).IsRequired().HasColumnName("\"Int\"");
            entity.Property(e => e.Wis).IsRequired();
            entity.Property(e => e.Agi).IsRequired();
            entity.Property(e => e.Con).IsRequired();
            entity.Property(e => e.Cha).IsRequired();

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
        // Note: Salt must be a Base-64 encoded string. The password hash was generated using Hash.HashPassword("acorn", "acorn", out salt)
        // with salt bytes: [188, 115, 125, 37, 197, 39, 213, 15, 169, 108, 40, 66, 176, 253, 213, 172]
        modelBuilder.Entity<Account>().HasData(
            new Account
            {
                Username = "acorn",
                Password = "1I+dieTmkT9qbF9YjSt1pkRvgAkAHqcStjRxOzuHwSc=",
                Salt = "vHN9JcUn1Q+pbChCsP3VrA==",
                FullName = "acorn",
                Location = "acorn",
                Email = "acorn@acorn-eo.dev",
                Country = "acorn",
                Created = new DateTime(2024, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                LastUsed = new DateTime(2024, 8, 31, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Note: Do NOT seed Character data here - HasConversion doesn't work with HasData
        // Character seeding is done in DbInitialiser after the database is created
    }
}