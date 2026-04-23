using System.Linq.Expressions;

namespace reservation.handson.lab.backend.domain.interfaces.repositories;

/// <summary>
/// Defines a read-only repository for retrieving entities from a data store.
/// </summary>
/// <typeparam name="T">The entity type that this repository handles</typeparam>
/// <typeparam name="TKey">The type of the primary key for the entity</typeparam>
public interface IReadOnlyRepository<T, TKey> where T : class, new()
{
    #region Synchronous Methods

    T Get(TKey id);
    IEnumerable<T> Get();
    IEnumerable<T> Get(Expression<Func<T, bool>> query);
    IEnumerable<T> Get(Expression<Func<T, bool>> query, int page, int pageSize, SortingCriteria sortingCriteria);
    int Count();
    int Count(Expression<Func<T, bool>> query);
    GetManyAndCountResult<T> GetManyAndCount(string? query, string defaultSorting);

    #endregion

    #region Asynchronous Methods

    Task<T> GetAsync(TKey id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> query, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>> query, CancellationToken cancellationToken = default);
    Task<GetManyAndCountResult<T>> GetManyAndCountAsync(string? query, string defaultSorting, CancellationToken cancellationToken = default);

    #endregion
}
