using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace HackerNews.Common;


/// <summary>
/// Service wraps the call to the extenal service. Provides thread safe access to resource as well
/// as caching.
/// </summary>
public class HackerNewsClientService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<int, byte> _cacheKeys = new(); // track keys because MemoryCache doesn't expose them
    private readonly IHackerNewsService _newsService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeSpan _semaphoreWaitTimeout;
    private readonly TimeSpan _cacheExpiryTimeout;
    private readonly int _maxStoryRequestCount;


    /// <summary>
    /// Inject Cache, newservice and optional configuration parameters. If not provided, default values will be used.
    /// </summary>
    public HackerNewsClientService(IMemoryCache cache, 
                                   IHackerNewsService newsService,
                                   TimeSpan? cacheExpiryTimeout = null,
                                   TimeSpan? semaphoreWaitTimeout = null,
                                   int? maxStoryRequestCount = null
        )
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _newsService = newsService ?? throw new ArgumentNullException(nameof(newsService));
        _semaphoreWaitTimeout = semaphoreWaitTimeout ?? TimeSpan.FromSeconds(5);
        _cacheExpiryTimeout = cacheExpiryTimeout ?? TimeSpan.FromSeconds(30);
        _maxStoryRequestCount = maxStoryRequestCount ?? 2000;
    }

    public async Task<Result> GetNewsItems(int count)
    {
        if (count <= 0)
        {
            return new Result(Enumerable.Empty<DetailedNewsItem>(), ResultStatus.Success, string.Empty);
        }

        if(count > _maxStoryRequestCount)
        {
            return new Result(Enumerable.Empty<DetailedNewsItem>(), ResultStatus.Failed, "Request exceeds maximum allowed stories");
        }

        //If cache contains enough stories then return them, otherwise acquire the semaphore and fetch more.
        var stories = GetCachedStories(count);
        if (stories.Count >= count)
        {
            return new Result(stories.OrderByDescending(i => i.score), ResultStatus.Success, string.Empty);
        }

        var acquired = await _semaphore.WaitAsync(_semaphoreWaitTimeout);
        if (!acquired)
        {
            throw new TimeoutException("Could not acquire news items. Try again later");
        }

        try
        {
            //Check the cache again after acquiring the semaphore in case another thread has already populated it.
            stories = GetCachedStories(count);
            if (stories.Count >= count)
            {
                return new Result(stories.OrderByDescending(i => i.score), ResultStatus.Success, string.Empty);
            }

            var bestStories = await _newsService.GetBestStoriesAsync() ?? Enumerable.Empty<int>();

            foreach (var story in bestStories)
            {
                if (stories.Count >= count)
                {
                    break;
                }

                //Check the story isn't already in the cache, if it is then ignore.
                if (!_cache.TryGetValue(story, out var cached))
                {
                    var details = await _newsService.GetStoryDetailsAsync(story);
                    if (details != null)
                    {
                        var options = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = _cacheExpiryTimeout
                        };

                        // Ensure keys are removed from our tracking dictionary when an entry is evicted
                        options.RegisterPostEvictionCallback((key, value, reason, state) =>
                        {
                            if (key is int s)
                            {
                                _cacheKeys.TryRemove(s, out _);
                            }
                        });

                        _cache.Set(story, details, options);
                        _cacheKeys[story] = 0;
                        stories.Add(details);
                    }
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return new Result(stories.OrderByDescending(i => i.score), ResultStatus.Success, string.Empty);
    }

    private List<DetailedNewsItem> GetCachedStories(int count)
    {
        var stories = new List<DetailedNewsItem>();

        // Iterate tracked keys — remove keys whose cache entries have expired
        foreach (var key in _cacheKeys.Keys.ToList())
        {
            if (_cache.TryGetValue(key, out var story) && story is DetailedNewsItem s)
            {
                stories.Add(s);
                if (stories.Count >= count)
                {
                    break;
                }
            }
            else
            {
                // Remove stale key so future enumerations are accurate
                _cacheKeys.TryRemove(key, out _);
            }
        }
        return stories;
    }
}