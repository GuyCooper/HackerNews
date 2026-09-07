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
    private readonly IHackerNewsService _newsService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeSpan _semaphoreWaitTimeout;
    private readonly TimeSpan _cacheExpiryTimeout;
    private readonly int _maxStoryRequestCount;
    private readonly int _maxDegreeOfParallelism;
    private readonly object _snapshotKey = new();

    /// <summary>
    /// Inject Cache, newservice and optional configuration parameters. If not provided, default values will be used.
    /// </summary>
    public HackerNewsClientService(IMemoryCache cache, 
                                   IHackerNewsService newsService,
                                   TimeSpan? cacheExpiryTimeout = null,
                                   TimeSpan? semaphoreWaitTimeout = null,
                                   int? maxStoryRequestCount = null,
                                   int? maxDegreeOfParallelism = null
        )
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _newsService = newsService ?? throw new ArgumentNullException(nameof(newsService));
        _semaphoreWaitTimeout = semaphoreWaitTimeout ?? TimeSpan.FromSeconds(5);
        _cacheExpiryTimeout = cacheExpiryTimeout ?? TimeSpan.FromSeconds(30);
        _maxStoryRequestCount = maxStoryRequestCount ?? 2000;
        _maxDegreeOfParallelism = maxDegreeOfParallelism  ?? 5;
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
        if (_cache.TryGetValue(_snapshotKey, out IReadOnlyList<DetailedNewsItem>? snapshot)
            && snapshot != null && snapshot.Count >= count)
        {
            return new Result(snapshot!.Take(count).ToArray(), ResultStatus.Success, string.Empty);
        }
                        
        var acquired = await _semaphore.WaitAsync(_semaphoreWaitTimeout);
        if (!acquired)
        {
            throw new TimeoutException("Could not acquire news items. Try again later");
        }

        try
        {
            //Check the cache again after acquiring the semaphore in case another thread has already populated it.
            if (_cache.TryGetValue(_snapshotKey, out snapshot)
                && snapshot != null && snapshot.Count >= count)
            {
                return new Result(snapshot!.Take(count).ToArray(), ResultStatus.Success, string.Empty);
            }

            var bestStories = await _newsService.GetBestStoriesAsync() ?? Enumerable.Empty<int>();

            var stories = new ConcurrentBag<(int Id, DetailedNewsItem Details)>();
            await Parallel.ForEachAsync(bestStories.Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async(id, _) =>
            {
                var details = await _newsService.GetStoryDetailsAsync(id);
                if (details != null)
                {
                    stories.Add((id, details));
                }
            });

            snapshot = Array.AsReadOnly(stories
                    .OrderByDescending(story => story.Details.score)
                    .ThenBy(story => story.Id)
                    .Select(story => story.Details)
                    .ToArray());
            
            // Publish only after every detail request succeeds; expiry begins here.
            _cache.Set(_snapshotKey, snapshot, _cacheExpiryTimeout);

            return new Result(snapshot!.Take(count).ToArray(), ResultStatus.Success, string.Empty);
        }
        finally
        {
            _semaphore.Release();
        }        
    }
}