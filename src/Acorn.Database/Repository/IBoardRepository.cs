using Acorn.Database.Models;

namespace Acorn.Database.Repository;

public interface IBoardRepository
{
    Task<IEnumerable<BoardPost>> GetPostsAsync(int boardId, int limit);
    Task<BoardPost?> GetPostAsync(int boardId, int postId);
    Task<int> GetRecentPostCountAsync(int boardId, string characterName, TimeSpan recentWindow);
    Task<int> GetTotalPostCountAsync(int boardId, string characterName, int limit);
    Task CreatePostAsync(BoardPost post);
    Task DeletePostAsync(int postId);
}
