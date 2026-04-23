namespace reservation.handson.lab.backend.domain.interfaces.repositories;

/// <summary>
/// Defines a full repository with both read and write operations.
/// </summary>
/// <typeparam name="T">The entity type that this repository handles</typeparam>
/// <typeparam name="TKey">The type of the primary key for the entity</typeparam>
public interface IRepository<T, TKey> : IReadOnlyRepository<T, TKey> where T : class, new()
{
    T Add(T item);
    Task AddAsync(T item);

    T Save(T item);
    Task SaveAsync(T item);

    void Delete(T item);
    Task DeleteAsync(T item);
}
