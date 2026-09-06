# HackerNews
clone project to local repo.
open command prompt and navigate to root folder

to build:
dotnet publish

to run unit tests:
dotnet test

to run:
dotnet run --project HackerNews.Web.Api

Open url:
http://localhost:5245

Enter a number of best stories to request. click Fetch.

Assumptions Made:

Length of time a "best story" is valid for. Cache expiry is set to 60 seconds. This can be changed in configuration.

Fewer than n stories may be returned

Max Length of time it takes to retrieve story details. Set for 10 seconds. This can be changed in the configuration.

Maximum number of stories available. Must have an upper bound on maximum number of stories that can be requested. set to 1000. This can be changed in the configuration.

Improvements:

1. Resiliency. Call to hackernews website should be made more resilient. This would include retries with backoff etc..

2. Make Cache Persistent. In real life the Cache expiry would probably be several hours or longer. In which case the cache should be persisted to DB \ disk.

3. Remove the cacheKeys concurrent Dictionary. updating cache operation should be atomic.

4. Add logging, Auditing etc...

5. Deploy in a docker container. Current solution requires user to have .NET SDK installed.

6. Other cross cutting concerns such as authentication \ authorisation \ load balancing. Should deploy behind reverse proxy \ Load Balancer for real work deployment.