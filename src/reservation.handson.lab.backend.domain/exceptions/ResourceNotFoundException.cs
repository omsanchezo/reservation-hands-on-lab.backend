namespace reservation.handson.lab.backend.domain.exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found in the system.
/// </summary>
public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message)
        : base(message)
    {
    }

    public ResourceNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
