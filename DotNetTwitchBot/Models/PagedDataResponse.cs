namespace DotNetTwitchBot.Models
{
    public class PagedDataResponse<T>
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; } = 0;
        public required List<T> Data { get; set; }
    }
}
