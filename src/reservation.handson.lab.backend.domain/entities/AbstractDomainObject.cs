using FluentValidation;
using FluentValidation.Results;

namespace reservation.handson.lab.backend.domain.entities;

public abstract class AbstractDomainObject
{
    protected AbstractDomainObject()
    { }

    protected AbstractDomainObject(Guid id, DateTime creationDate)
    {
        Id = id;
        CreationDate = creationDate;
    }

    public virtual Guid Id { get; set; } = Guid.NewGuid();
    public virtual DateTime CreationDate { get; set; } = DateTime.Now;

    public virtual bool IsValid()
    {
        IValidator? validator = GetValidator();
        if (validator == null)
            return true;

        var context = new ValidationContext<object>(this);
        ValidationResult result = validator.Validate(context);
        return result.IsValid;
    }

    public virtual IEnumerable<ValidationFailure> Validate()
    {
        IValidator? validator = GetValidator();
        if (validator == null)
            return new List<ValidationFailure>();
        else
        {
            var context = new ValidationContext<object>(this);
            ValidationResult result = validator.Validate(context);
            return result.Errors;
        }
    }

    public virtual IValidator? GetValidator()
         => null;
}
