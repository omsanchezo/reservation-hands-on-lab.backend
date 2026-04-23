namespace reservation.handson.lab.backend.domain.interfaces.repositories;

/// <summary>
/// Result container for paginated queries with sorting.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public class GetManyAndCountResult<T>
{
    public const int DEFAULT_PAGE_SIZE = 25;

    public IEnumerable<T> Items { get; set; }
    public long Count { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public SortingCriteria Sorting { get; set; }

    public GetManyAndCountResult(IEnumerable<T> items, long count, int pageNumber, int pageSize, SortingCriteria sorting)
    {
        Items = items;
        Count = count;
        PageNumber = pageNumber;
        PageSize = pageSize;
        Sorting = sorting;
    }

    public GetManyAndCountResult()
    {
        Items = [];
        Count = 0;
        PageNumber = 1;
        PageSize = DEFAULT_PAGE_SIZE;
        Sorting = new SortingCriteria();
    }
}
