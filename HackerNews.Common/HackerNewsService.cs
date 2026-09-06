using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace HackerNews.Common
{
    public interface IHackerNewsService
    {
        Task<IEnumerable<int>?> GetBestStoriesAsync();

        Task<DetailedNewsItem?> GetStoryDetailsAsync(int storyId);
    }

    /// <summary>
    /// Concreate HackerNews service makes actual call to HackerNews website
    /// </summary>
    public class HackerNewsService : IHackerNewsService
    {
        private readonly HttpClient client = new HttpClient { BaseAddress = new Uri("https://hacker-news.firebaseio.com") };

        public async Task<IEnumerable<int>?> GetBestStoriesAsync()
        {
            var result = await client.GetStreamAsync("v0/beststories.json");
            using var reader = new StreamReader(result);
            var resultStr = reader.ReadToEnd();
            return JsonSerializer.Deserialize<IEnumerable<int>>(resultStr);
        }

        public async Task<DetailedNewsItem?> GetStoryDetailsAsync(int storyId)
        {
            var result = await client.GetStreamAsync($"v0/item/{storyId}.json");
            using var reader = new StreamReader(result);
            var resultStr = reader.ReadToEnd();
            if (resultStr != null)
            {
                var jitem = JObject.Parse(resultStr);
                return new DetailedNewsItem
                    (
                    title: jitem["title"]?.Value<string>(),
                    url: jitem["url"]?.Value<string>(),
                    postedBy: jitem["by"]?.Value<string>(),
                    time: jitem["time"]?.Value<string>(),
                    score: jitem["score"]?.Value<int>(),
                    commentCount: jitem["descendants"]?.Value<int>()
                    );
            }
            return null;
        }
    }
}
