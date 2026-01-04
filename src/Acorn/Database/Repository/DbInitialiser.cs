using Acorn.Database.Models;
using Acorn.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Database.Repository;

public class DbInitialiser : IDbInitialiser
{
    private readonly AcornDbContext _context;
    private readonly ILogger<DbInitialiser> _logger;

    public DbInitialiser(AcornDbContext context, ILogger<DbInitialiser> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring database is created...");
            
            // EnsureCreatedAsync creates the database schema if it doesn't exist
            // Note: This won't apply migrations, use MigrateAsync() if you add EF migrations
            var created = await _context.Database.EnsureCreatedAsync();
            if (created)
            {
                _logger.LogInformation("Database schema created successfully");
            }
            else
            {
                _logger.LogInformation("Database schema already exists");
            }
            
            _logger.LogInformation("Database initialized successfully");

            // Seed default account and character if they don't exist
            await SeedDefaultDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }

    private async Task SeedDefaultDataAsync()
    {
        // Verify tables exist before attempting to seed data
        Account? acornAccount = null;
        try
        {
            // Check if acorn account exists
            acornAccount = await _context.Accounts
                .Include(a => a.Characters)
                .FirstOrDefaultAsync(a => a.Username == "acorn");
        }
        catch (Exception ex) when (ex.Message.Contains("doesn't exist") || ex.Message.Contains("does not exist"))
        {
            _logger.LogWarning("Tables don't exist yet. Creating schema...");
            
            // If tables don't exist, create them explicitly using the generated script
            try
            {
                var sqlScript = _context.Database.GenerateCreateScript();
                if (!string.IsNullOrEmpty(sqlScript))
                {
                    await _context.Database.ExecuteSqlRawAsync(sqlScript);
                    _logger.LogInformation("Database schema created successfully using SQL script");
                }
            }
            catch (Exception createEx)
            {
                _logger.LogError(createEx, "Failed to create database schema");
                throw;
            }
            
            // Retry the query after creating tables
            acornAccount = await _context.Accounts
                .Include(a => a.Characters)
                .FirstOrDefaultAsync(a => a.Username == "acorn");
        }

        if (acornAccount == null)
        {
            _logger.LogInformation("Creating default 'acorn' account...");

            // Create account with hashed password
            var passwordHash = Hash.HashPassword("acorn", "acorn", out var salt);
            var saltString = Convert.ToBase64String(salt);

            acornAccount = new Account
            {
                Username = "acorn",
                Password = passwordHash,
                Salt = saltString,
                FullName = "Acorn Admin",
                Location = "Acorn Server",
                Email = "acorn@acorn-eo.dev",
                Country = "Acorn",
                Created = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow,
                Characters = new List<Character>()
            };

            _context.Accounts.Add(acornAccount);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Default 'acorn' account created");
        }

        // Check if acorn character exists
        var acornCharacter = await _context.Characters
            .Include(c => c.Items)
            .Include(c => c.Paperdoll)
            .FirstOrDefaultAsync(c => c.Name == "acorn");

        if (acornCharacter == null)
        {
            _logger.LogInformation("Creating default 'acorn' character...");

            var character = new Character
            {
                Accounts_Username = "acorn",
                Name = "acorn",
                Title = "Admin",
                Home = "Aeven",
                Fiance = "",
                Partner = "",
                Admin = AdminLevel.HighGameMaster,
                Class = 1,
                Gender = Gender.Male,
                Race = 1,
                HairStyle = 3,
                HairColor = 8,
                Map = 192,
                X = 6,
                Y = 6,
                Direction = Direction.Down,
                Level = 100,
                Exp = 0,
                MaxHp = 100,
                Hp = 100,
                MaxTp = 100,
                Tp = 100,
                MaxSp = 100,
                Sp = 100,
                Str = 100,
                Wis = 100,
                Agi = 100,
                Con = 100,
                Cha = 100,
                MinDamage = 100,
                MaxDamage = 200,
                MaxWeight = 100,
                StatPoints = 0,
                SkillPoints = 0,
                Karma = 0,
                SitState = SitState.Stand,
                Hidden = false,
                NoInteract = false,
                BankMax = 100,
                GoldBank = 0,
                Usage = 0,
                Items = new List<CharacterItem>(),
            };

            var paperdoll = new CharacterPaperdoll
            {
                CharacterName = "acorn",
                Hat = 313,
                Armor = 428,
                Weapon = 421,
                Shield = 384,
                Boots = 382,
                Necklace = 50,
                Belt = 0,
                Gloves = 0,
                Accessory = 0,
                Ring1 = 0,
                Ring2 = 0,
                Bracer1 = 0,
                Bracer2 = 0,
                Armlet1 = 0,
                Armlet2 = 0
            };

            character.Paperdoll = paperdoll;
            
            _context.Characters.Add(character);
            _context.CharacterPaperdolls.Add(paperdoll);
            
            // DEBUG: Log the Gender value before saving
            _logger.LogInformation("DEBUG: About to save character with Gender = {Gender} ({GenderInt})", 
                character.Gender, (int)character.Gender);
            
            await _context.SaveChangesAsync();
            
            // DEBUG: Re-read from database to verify
            var saved = await _context.Characters.FindAsync("acorn");
            _logger.LogInformation("DEBUG: After save, database has Gender = {Gender} ({GenderInt})", 
                saved?.Gender, (int)(saved?.Gender ?? 0));
            
            _logger.LogInformation("Default 'acorn' character created");
        }
    }
}

public interface IDbInitialiser
{
    Task InitialiseAsync();
}