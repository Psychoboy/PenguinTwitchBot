namespace DotNetTwitchBot.Models
{
    public class PaginationFilter
    {
        public int Page { get; set; } = 0;
        public int Count { get; set; } = 10;
        public string Filter { get; set; } = "";
        public PaginationFilter() { }
        public PaginationFilter(int page, int count)
        {
            Page = page;
            Count = count;
        }
    }
}
