using System.Collections.Generic;

namespace APLabApp.BLL
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int Page { get; }
        public int PageSize { get; }
        public int Total { get; }
        public int TotalPages { get; }

        public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int total, int totalPages)
        {
            Items = items;
            Page = page;
            PageSize = pageSize;
            Total = total;
            TotalPages = totalPages;
        }
    }
}
