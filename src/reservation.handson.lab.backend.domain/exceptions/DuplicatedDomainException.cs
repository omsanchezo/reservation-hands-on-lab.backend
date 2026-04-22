namespace reservation.handson.lab.backend.domain.exceptions;

/// <summary>
/// Exception thrown when attempting to create or update an entity that would result in a duplicate.
/// </summary>
public class DuplicatedDomainException : Exception
{
    public DuplicatedDomainException(string message)
        : base(message)
    {
    }

    public DuplicatedDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
