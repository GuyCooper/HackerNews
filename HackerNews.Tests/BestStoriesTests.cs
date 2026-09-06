using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using HackerNews.Common;
using System.Threading.Tasks;

namespace HackerNews.Tests
{
    public class HackerNewsClientServiceTests
    {
        private class FakeNewsService : IHackerNewsService
        {
            public IList<int> BestStories { get; init; } = new List<int>();
            public ConcurrentDictionary<int, int> DetailsCallCount { get; } = new();
            public TimeSpan DetailsDelay { get; init; } = TimeSpan.Zero;

            public Task<IEnumerable<int>?> GetBestStoriesAsync()
            {
                return Task.FromResult<IEnumerable<int>?>(BestStories);
            }

            public async Task<DetailedNewsItem?> GetStoryDetailsAsync(int storyId)
            {
                DetailsCallCount.AddOrUpdate(storyId, 1, (_, v) => v + 1);
                if (DetailsDelay > TimeSpan.Zero)
                {
                    await Task.Delay(DetailsDelay);
                }
                return new DetailedNewsItem("details:{storyId}",null,null,null,null,null);
            }
        }

        [Fact]
        public async Task CachingBehavior_UsesCacheOnSecondCall()
        {
            var fake = new FakeNewsService
            {
                BestStories = new List<int> { 1,2,3 }
            };

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var svc = new HackerNewsClientService(memoryCache, fake);

            var r1 = await svc.GetNewsItems(2);
            Assert.Equal(2, r1.NewsItems.Count());
            Assert.Equal(2, fake.DetailsCallCount.Values.Sum());

            var r2 = await svc.GetNewsItems(2);
            Assert.Equal(2, r2.NewsItems.Count());
            // No additional detail fetches on second call
            Assert.Equal(2, fake.DetailsCallCount.Values.Sum());
        }

        [Fact]
        public async Task SemaphoreTimeout_ThrowsTimeoutWhenContention()
        {
            var fake = new FakeNewsService
            {
                BestStories = new List<int> { 1,2,3 },
                DetailsDelay = TimeSpan.FromMilliseconds(500) // make the first caller hold the semaphore
            };

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            // Set very short timeout so second caller will time out quickly
            var svc = new HackerNewsClientService(memoryCache, fake, semaphoreWaitTimeout: TimeSpan.FromMilliseconds(50));

            // Start a long-running fetch that will hold the semaphore
            var t1 = Task.Run(() => svc.GetNewsItems(3));

            // Give t1 a little time to start and acquire the semaphore
            await Task.Delay(10);

            // Second caller should time out
            await Assert.ThrowsAsync<TimeoutException>(() => svc.GetNewsItems(3));

            // Ensure the long-running task completes before finishing the test
            await t1;
        }

        [Fact]
        public async Task Concurrency_OnlySingleFetchPerStory()
        {
            var fake = new FakeNewsService
            {
                BestStories = new List<int> { 1,2,3,4,5 },
                DetailsDelay = TimeSpan.FromMilliseconds(100)
            };

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var svc = new HackerNewsClientService(memoryCache, fake);

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(() => svc.GetNewsItems(5)))
                .ToArray();

            await Task.WhenAll(tasks);

            // Each story should be fetched exactly once
            Assert.Equal(5, fake.DetailsCallCount.Count);
            Assert.All(fake.DetailsCallCount.Values, v => Assert.Equal(1, v));

            // Each task should have received 5 items
            foreach (var t in tasks)
            {
                var result = await t;
                Assert.Equal(5, result.NewsItems.Count());
            }
        }

        [Fact]
        public async Task CacheExpiry_ExpiresAfterDuration()
        {
            var fake = new FakeNewsService
            {
                BestStories = new List<int> { 1,2,3 }
            };
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var svc = new HackerNewsClientService(memoryCache, fake, cacheExpiryTimeout: TimeSpan.FromSeconds(1));
            // First call to populate cache
            var r1 = await svc.GetNewsItems(2);
            Assert.Equal(2, r1.NewsItems.Count());
            Assert.Equal(2, fake.DetailsCallCount.Values.Sum());
            // Wait for cache to expire (assuming default expiration is set in the service)
            await Task.Delay(TimeSpan.FromSeconds(2));
            // Second call should fetch details again since cache expired
            var r2 = await svc.GetNewsItems(2);
            Assert.Equal(2, r2.NewsItems.Count());
            Assert.Equal(4, fake.DetailsCallCount.Values.Sum()); // Should have fetched details again
        }
    }
}