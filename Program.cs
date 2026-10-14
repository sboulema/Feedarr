using Feedarr.Helpers;
using System.Web;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStaticFiles();

var baseUrl = app.Configuration["BASE_URL"] ?? string.Empty;

// Serves an RSS feed containing one item per .torrent/.magnet file in /torrents/{folder}.
app.MapGet("/{folder}.rss", async (string folder) =>
{
    var directory = FeedHelper.GetFolder(folder);

    if (directory is null)
    {
        return Results.NotFound();
    }

    var entries = await FeedHelper.GetEntries(baseUrl, folder, directory);
    var feed = FeedHelper.BuildFeed(baseUrl, folder, entries);

    return Results.Text(FeedHelper.FeedToByteArray(feed), "application/rss+xml; charset=utf-8");
});

// Serves an HTML page listing the same entries as the RSS feed.
app.MapGet("/{folder}", async (string folder) =>
{
    var directory = FeedHelper.GetFolder(folder);

    if (directory is null)
    {
        return Results.NotFound();
    }

    var entries = await FeedHelper.GetEntries(baseUrl, folder, directory);

    return Results.Content(FeedHelper.RenderHtml(baseUrl, folder, entries), "text/html; charset=utf-8");
});

// Serves a single .torrent file from /torrents/{folder}.
app.MapGet("/{folder}/{fileName}.torrent", async (string folder, string fileName) =>
{
    var directory = FeedHelper.GetFolder(folder);
    var name = Path.GetFileName(HttpUtility.UrlDecode(fileName));
    var path = directory is null ? null : Path.Combine(directory.FullName, $"{name}.torrent");

    return path is not null && File.Exists(path)
        ? Results.File(await File.ReadAllBytesAsync(path))
        : Results.NotFound();
});

app.Run();