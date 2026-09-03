using System.Text.Json;

namespace HackerNews.Common;
{
    public interface IHackerNewsService
    {
        Task<IEnumerable<string>?> GetBestStoriesAsync();

        Task<string?> GetStoryDetailsAsync(string storyId);
    }

    /// <summary>
    /// Concreate HackerNews service makes actual call to HackerNews website
    /// </summary>
    public class HackerNewsService : IHackerNewsService
    {
        private readonly HttpClient client = new HttpClient { BaseAddress = new Uri("")};

        public async Task<IEnumerable<string>?> GetBestStoriesAsync()
        {
            var result = await client.GetStringAsync("v0/beststories.json");
            return JsonSerializer.Deserialize<IEnumerable<string>>(result);
        }

        public async Task<string?> GetStoryDetailsAsync(string storyId)
        {
            return await client.GetStringAsync("v0/beststories.json");
        }
    }
}
