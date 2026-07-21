namespace CallCadence.Domain.Paging
{
    public class Paging
    {
        public int PageCount { get; set; }
        public int TotalItems { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public Paging(int pageCount, int totalItems, int pageNumber, int pageSize)
        {
            PageCount = pageCount;
            TotalItems = totalItems;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }
    }
}
