using FluentValidation.Results;

namespace reservation.handson.lab.backend.domain.exceptions;

public class InvalidDomainException : Exception
{
    public IEnumerable<ValidationFailure> Errors { get; set; }

    public InvalidDomainException(IEnumerable<ValidationFailure> errors)
        : base("Domain validation failed")
    {
        Errors = errors;
    }

    public InvalidDomainException(string message)
        : base(message)
    {
        Errors = [];
    }
}
