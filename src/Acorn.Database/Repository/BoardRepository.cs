using Acorn.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

public class BoardRepository : IBoardRepository
{
    private readonly AcornDbContext _context;
    private readonly ILogger<BoardRepository> _logger;

    public BoardRepository(AcornDbContext context, ILogger<BoardRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<BoardPost>> GetPostsAsync(int boardId, int limit)
    {
        try
        {
            return await _context.BoardPosts
                .Where(p => p.BoardId == boardId)
                .OrderByDescending(p => p.Id)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching posts for board {BoardId}", boardId);
            return [];
        }
    }

    public async Task<BoardPost?> GetPostAsync(int boardId, int postId)
    {
        try
        {
            return await _context.BoardPosts
                .FirstOrDefaultAsync(p => p.BoardId == boardId && p.Id == postId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching post {PostId} from board {BoardId}", postId, boardId);
            return null;
        }
    }

    public async Task<int> GetRecentPostCountAsync(int boardId, string characterName, TimeSpan recentWindow)
    {
        try
        {
            var cutoff = DateTime.UtcNow - recentWindow;
            return await _context.BoardPosts
                .CountAsync(p => p.BoardId == boardId &&
                                 p.CharacterName == characterName &&
                                 p.CreatedAt >= cutoff);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting recent post count for {CharacterName} on board {BoardId}",
                characterName, boardId);
            return 0;
        }
    }

    public async Task<int> GetTotalPostCountAsync(int boardId, string characterName, int limit)
    {
        try
        {
            // Count posts by this character within the most recent 'limit' posts on this board
            var recentPostIds = await _context.BoardPosts
                .Where(p => p.BoardId == boardId)
                .OrderByDescending(p => p.Id)
                .Take(limit)
                .Select(p => p.Id)
                .ToListAsync();

            return await _context.BoardPosts
                .CountAsync(p => recentPostIds.Contains(p.Id) && p.CharacterName == characterName);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting total post count for {CharacterName} on board {BoardId}",
                characterName, boardId);
            return 0;
        }
    }

    public async Task CreatePostAsync(BoardPost post)
    {
        try
        {
            await _context.BoardPosts.AddAsync(post);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created post '{Subject}' on board {BoardId} by {CharacterName}",
                post.Subject, post.BoardId, post.CharacterName);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating post on board {BoardId}", post.BoardId);
            throw;
        }
    }

    public async Task DeletePostAsync(int postId)
    {
        try
        {
            var post = await _context.BoardPosts.FindAsync(postId);
            if (post != null)
            {
                _context.BoardPosts.Remove(post);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted post {PostId} from board {BoardId}", postId, post.BoardId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error deleting post {PostId}", postId);
            throw;
        }
    }
}
