using System.Text.RegularExpressions;
using X.Web.RSS;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{folder}.rss", (string folder) =>
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
            Items = new()
        }
    };

    foreach (var file in new DirectoryInfo($"/torrents/{folder}").GetFiles())
    {
        feed.Channel.Items.Add(new()
        {
            Title = Cleanup(file.Name),
            Description = Cleanup(file.Name),
            Link = new($"{baseUrl}/{folder}/{file.Name}"),
            PubDate = file.CreationTimeUtc,
            Enclosure = new()
            {
                Url = new($"{baseUrl}/{folder}/{file.Name}"),
                Type = "application/x-bittorrent"
            }
        });
    }
    
    return Results.Text(feed.ToXml(), "text/xml");
});

app.MapGet("/{folder}/{file}.torrent", async (string folder, string file) =>
{
    if (!Path.Exists($"/torrents/{folder}/{file}.torrent"))
    {
        return Results.NotFound();
    }

    return Results.File(await File.ReadAllBytesAsync($"/torrents/{folder}/{file}.torrent"));
});

app.Run();

string Cleanup(string fileName)
{
    fileName = Path.GetFileNameWithoutExtension(fileName);
    fileName = fileName.Replace(".", " ");
    fileName = Regex.Replace(fileName, @"\p{Cs}|\p{So}", string.Empty);
    fileName = fileName.Trim();

    return fileName;
}