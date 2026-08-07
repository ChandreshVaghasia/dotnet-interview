namespace TodoApi.Models
{
    /// <summary>
    /// Generic paginated result.
    /// </summary>
    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }

        public PaginatedResult() { }

        public PaginatedResult(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
        {
            Items = items.ToList();
            TotalCount = totalCount;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalPages = pageSize <= 0 ? 0 : (int)System.Math.Ceiling(totalCount / (double)pageSize);
        }
    }
}