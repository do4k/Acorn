namespace Acorn.Database.Repository;

public interface IDbRepository<T> : IDbRepository<T, string> where T : class
{
}

public interface IDbRepository<T, in TKey> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByKeyAsync(TKey key);
    Task CreateAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
