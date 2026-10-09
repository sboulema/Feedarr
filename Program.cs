using Feedarr.Helpers;
using System.ServiceModel.Syndication;
using System.Web;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStaticFiles();

var baseUrl = app.Configuration["BASE_URL"] ?? string.Empty;

// Serves an RSS feed containing one item per .torrent/.magnet file in /torrents/{folder}.
app.MapGet("/{folder}.rss", async (string folder) =>
{
    if (!Path.Exists($"{FeedHelper.TorrentsRoot}/{folder}"))
    {
        return Results.NotFound();
    }

    var items = await Task.WhenAll(
        new DirectoryInfo($"{FeedHelper.TorrentsRoot}/{folder}")
            .GetFiles()
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(async file =>
            {
                var title = FeedHelper.Cleanup(file.Name);
                var link = await FeedHelper.GetItemUri(baseUrl, folder, file.Name);
                var enclosureLink = await FeedHelper.CreateMediaEnclosureLink(baseUrl, folder, file);

                var item = new SyndicationItem(title, title, link, FeedHelper.GetItemId(title), file.CreationTimeUtc)
                {
                    PublishDate = file.CreationTimeUtc
                };
                item.Links.Add(enclosureLink);

                return item;
            }));

    var feed = new SyndicationFeed(
        title: $"Feedarr {folder} feed",
        description: $"Feedarr {folder} feed",
        feedAlternateLink: new($"{baseUrl}/{folder}.rss"),
        items: items)
    {
        ImageUrl = new Uri($"{baseUrl}/favicon-512.png"),
        TimeToLive = TimeSpan.FromMinutes(30),
        LastUpdatedTime = items
            .Select(item => item.PublishDate)
            .DefaultIfEmpty(DateTimeOffset.UtcNow)
            .Max()
    };

    return Results.Text(FeedHelper.FeedToByteArray(feed), "application/rss+xml; charset=utf-8");
});

// Serves a single .torrent file from /torrents/{folder}.
app.MapGet("/{folder}/{fileName}.torrent", async (string folder, string fileName) =>
{
    var path = $"{FeedHelper.TorrentsRoot}/{folder}/{HttpUtility.UrlDecode(fileName)}.torrent";

    return Path.Exists(path)
        ? Results.File(await File.ReadAllBytesAsync(path))
        : Results.NotFound();
});

app.Run();