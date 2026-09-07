    public class HackerNewsConfiguration
    {
        public int? CacheExpiryTimeoutSeconds { get; set; }

        public int? SemaphoreWaitTimeoutSeconds { get; set; }

        public int? MaxStoryRequestCount { get; set; }

        public int? MaxDegreeOfParallelism { get; set; }
    }

