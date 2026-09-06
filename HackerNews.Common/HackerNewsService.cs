using System.Text.Json;

namespace HackerNews.Common
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
            var result = await client.GetStreamAsync("v0/beststories.json");
            using var reader = new StreamReader(result);
            return JsonSerializer.Deserialize<IEnumerable<string>>(reader.ReadToEnd());
        }

        public async Task<string?> GetStoryDetailsAsync(string storyId)
        {
            var result = await client.GetStringAsync($"v0/item/{storyId}.json");
            using var reader = new StreamReader(result);
            return reader.ReadToEnd();
        }
    }
}
