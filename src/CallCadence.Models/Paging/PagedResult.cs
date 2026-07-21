
namespace CallCadence.Domain.Paging
{
    public class PagedResult<T>
    {
        public Paging Paging { get; set; } = new Paging(0, 0, 0, 0);
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

        public PagedResult(Paging paging, IEnumerable<T> items)
        {
            Paging = paging;
            Items = items;
        }
    }
}
