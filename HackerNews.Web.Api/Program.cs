
using HackerNews.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<HackerNewsConfiguration>(
    builder.Configuration.GetSection(nameof(HackerNewsConfiguration)));

builder.Services.AddMemoryCache();
builder.Services.AddTransient<IHackerNewsService, HackerNewsService>();
builder.Services.AddSingleton(c =>
{
    var configuration = c.GetRequiredService<IOptions<HackerNewsConfiguration>>().Value;
    var cache = c.GetRequiredService<IMemoryCache>();
    var newsService = c.GetRequiredService<IHackerNewsService>();

    return new HackerNewsClientService(cache, newsService,
        configuration.CacheExpiryTimeoutSeconds.HasValue ? TimeSpan.FromSeconds(configuration.CacheExpiryTimeoutSeconds.Value) : null,
        configuration.SemaphoreWaitTimeoutSeconds.HasValue ? TimeSpan.FromSeconds(configuration.SemaphoreWaitTimeoutSeconds.Value) : null,
        configuration.MaxStoryRequestCount);
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/beststories", 
    (HackerNewsClientService newsService, int count) =>
{
    return newsService.GetNewsItems(count);
})
.WithName("GetBestStories")
.WithOpenApi();

app.Run();

