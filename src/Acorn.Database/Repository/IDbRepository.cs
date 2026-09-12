namespace Acorn.Database.Repository;

public interface IDbRepository<T> : IDbRepository<T, string> where T : class
{
}
