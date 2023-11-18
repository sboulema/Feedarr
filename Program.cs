using System.Text.RegularExpressions;
using X.Web.RSS;
using X.Web.RSS.Structure.Validators;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{folder}.rss", async (string folder) =>
{
    if (!Path.Exists($"/torrents/{folder}"))
    {
        return Results.NotFound();
    }

    var baseUrl = app.Configuration["BASE_URL"] ?? string.Empty;

    var feed = new RssDocument
    {
        Channel = new()
        {
            Title = $"Feedarr {folder} feed",
            Link = new(baseUrl),
            TTL = 30,
            Description = $"Feedarr {folder} feed",
            Items = []
        }
    };

    foreach (var file in new DirectoryInfo($"/torrents/{folder}").GetFiles())
    {
        feed.Channel.Items.Add(new()
        {
            Title = Cleanup(file.Name),
            Description = Cleanup(file.Name),
            Link = await GetRssUrl(baseUrl, folder, file.Name),
            PubDate = file.CreationTimeUtc,
            Enclosure = new()
            {
                Url = await GetRssUrl(baseUrl, folder, file.Name),
                Type = GetType(file.Extension)
            }
        });
    }
    
    return Results.Text(feed.ToXml(), "text/xml");
});

app.MapGet("/{folder}/{fileName}.torrent", async (string folder, string fileName) =>
{
    if (!Path.Exists($"/torrents/{folder}/{fileName}.torrent"))
    {
        return Results.NotFound();
    }

    return Results.File(await File.ReadAllBytesAsync($"/torrents/{folder}/{fileName}.torrent"));
});

app.Run();

string Cleanup(string fileName)
{
    fileName = Path.GetFileNameWithoutExtension(fileName);
    fileName = fileName.Replace(".", " ");
    // Remove emoticons from file name
    fileName = Regex.Replace(fileName, @"\p{Cs}|\p{So}", string.Empty);
    fileName = fileName.Trim();

    return fileName;
}

async Task<RssUrl> GetRssUrl(string baseUrl, string folder, string fileName)
{
    var fileExtension = Path.GetExtension(fileName);

    if (fileExtension == ".torrent")
    {
        return new($"{baseUrl}/{folder}/{fileName}");
    }

    if (fileExtension == ".magnet")
    {
        var magnetUrl = await File.ReadAllTextAsync($"/torrents/{folder}/{fileName}");
        return new(magnetUrl);
    }

    return new();
}

string GetType(string fileExtension)
    => fileExtension switch
    {
        ".torrent" => "application/x-bittorrent",
        ".magnet" => "x-scheme-handler/magnet",
        _ => string.Empty
    };