namespace reservation.handson.lab.backend.domain.interfaces.repositories;

/// <summary>
/// Coordinates transactions and provides access to all repositories.
/// Add repository properties here as you create new features.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // -- Add your repository properties here --
    // Example:
    // IUserRepository Users { get; }
    // IOrderRepository Orders { get; }

    void BeginTransaction();
    void Commit();
    void Flush();
    void Rollback();
    void ResetTransaction();
    bool IsActiveTransaction();
}
