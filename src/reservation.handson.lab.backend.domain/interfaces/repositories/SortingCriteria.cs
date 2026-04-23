namespace reservation.handson.lab.backend.domain.interfaces.repositories;

/// <summary>
/// Represents a sorting criteria for paginated queries.
/// </summary>
public class SortingCriteria
{
    public string SortBy { get; set; } = string.Empty;
    public SortingCriteriaType Criteria { get; set; } = SortingCriteriaType.Ascending;

    public SortingCriteria() { }

    public SortingCriteria(string sortBy)
    {
        this.SortBy = sortBy;
    }

    public SortingCriteria(string sortBy, SortingCriteriaType criteria)
    {
        this.SortBy = sortBy;
        this.Criteria = criteria;
    }
}

public enum SortingCriteriaType
{
    Ascending = 1,
    Descending = 2
}
