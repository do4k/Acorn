using Acorn.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

public class CharacterRepository : IDbRepository<Character>
{
    private readonly AcornDbContext _context;
    private readonly ILogger<CharacterRepository> _logger;

    public CharacterRepository(
        AcornDbContext context,
        ILogger<CharacterRepository> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateAsync(Character entity)
    {
        try
        {
            await _context.Characters.AddAsync(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error saving character information for {CharacterName}", entity.Name);
            throw;
        }
    }

    public async Task DeleteAsync(Character entity)
    {
        try
        {
            _context.Characters.Remove(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error deleting character {CharacterName}", entity.Name);
            throw;
        }
    }

    public async Task<Character?> GetByKeyAsync(string name)
    {
        try
        {
            var character = await _context.Characters
                .Include(c => c.Items)
                .Include(c => c.Paperdoll)
                .FirstOrDefaultAsync(c => c.Name == name);

            if (character is null)
            {
                _logger.LogInformation("Character {Character} not found", name);
                return null;
            }

            return character;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching character {Character}", name);
            return null;
        }
    }

    public async Task UpdateAsync(Character entity)
    {
        try
        {
            // Load existing character with related entities
            var existingCharacter = await _context.Characters
                .Include(c => c.Items)
                .Include(c => c.Paperdoll)
                .FirstOrDefaultAsync(c => c.Name == entity.Name);

            if (existingCharacter == null)
            {
                _logger.LogWarning("Character '{Name}' not found for update", entity.Name);
                return;
            }

            // Update character properties
            _context.Entry(existingCharacter).CurrentValues.SetValues(entity);

            // Update Items - remove old ones and add new ones
            _context.CharacterItems.RemoveRange(existingCharacter.Items);
            existingCharacter.Items.Clear();
            foreach (var item in entity.Items)
            {
                existingCharacter.Items.Add(item);
            }

            // Update Paperdoll
            if (existingCharacter.Paperdoll != null && entity.Paperdoll != null)
            {
                _context.Entry(existingCharacter.Paperdoll).CurrentValues.SetValues(entity.Paperdoll);
            }
            else if (entity.Paperdoll != null)
            {
                existingCharacter.Paperdoll = entity.Paperdoll;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Saved character '{Name}'", entity.Name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error updating character information for {CharacterName}", entity.Name);
            throw;
        }
    }

    public async Task<IEnumerable<Character>> GetAllAsync()
    {
        try
        {
            return await _context.Characters
                .Include(c => c.Items)
                .Include(c => c.Paperdoll)
                .ToListAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching all characters");
            return [];
        }
    }
}
