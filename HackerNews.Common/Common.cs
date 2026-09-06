
namespace HackerNews.Common
{
    public enum ResultStatus
    {
        Success,
        Failed
    }

    public record DetailedNewsItem(string? title, string? url, string? postedBy, string? time, int? score, int? commentCount);

    public record Result(IEnumerable<DetailedNewsItem> NewsItems, ResultStatus Status, string Reason);

}
