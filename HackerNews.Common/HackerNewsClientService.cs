using Microsoft.Extensions.Caching.Memory;

namespace HackerNews.Common;

//Vendor supplied exchange rate service interface
public interface IExchangeRateService
{
    Task<decimal?> GetSpotRateAsync(string code);
}

public enum ResultStatus
{
   Success,
   Failed
}

public record Result(IEnumerable<string>NewsItems, ResultStatus Status, string Reason );

/// <summary>
/// Service wraps the call to the extenal service. Provides thread safe access to resource as well
/// as caching.
/// </summary>
public class HackerNewsClientService
{
    private readonly MemoryCache _cache;
    private readonly IHackerNewsService _newsService;
    private readonly SemaphoreSlim _semaphore = new(1, 1); // Single sempaphore is acceptable but semaphore per currency reduces contention

    public HackerNewsClientService(MemoryCache cache, IHackerNewsService newsService)
    {
        _cache = cache;
        _newsService = newsService;
    }
    
    public  async Task<Result> GetNewsItems(int count)
    {
        var stories = GetCachedStories(count);
        if (stories.Count >= count)
        {
            return new Result(stories, ResultStatus.Success, "");
        }
        
        var acquired = await _semaphore.WaitAsync(TimeSpan.FromSeconds(5));
        if (!acquired)
        {
            throw new TimeoutException("Could not acquired exchange rates. Try again later");
        }

        try
        {
            stories = GetCachedStories(count);
            if (stories.Count >= count)
            {
                return new Result(stories, ResultStatus.Success, "");
            }

            var bestStories = await _newsService.GetBestStoriesAsync();

            foreach(var story in bestStories)
            {
                if(stories.Count >= count)
                {
                    // we have the requested amount of stories so can return
                    break;
                }

                //Check the story isn't already in the cache, if it is then ignore.
                if(!_cache.TryGetValue(story, out var cached))
                {
                    var details = await _newsService.GetStoryDetailsAsync(story);
                    if (details != null)
                    {
                        _cache.Set(story, details, new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromSeconds(30)));
                        stories.Add(details);
                    }
                }                
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return new Result(stories, ResultStatus.Success, string.Empty);
    }

    private List<string> GetCachedStories(int count)
    {
        var stories = new List<string>();
        var keys = _cache.Keys.ToList();
        foreach (var key in keys)
        {
            if (_cache.TryGetValue(key, out var story) && story != null)
            {
                stories.Add((string)story);
                if (stories.Count >= count)
                {
                    break;
                }
            }
        }
        return stories;
    }
}
