using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{folder}.rss", async (string folder) =>
{
	if (!Path.Exists($"/torrents/{folder}"))
	{
		return Results.NotFound();
	}

	var baseUrl = app.Configuration["BASE_URL"] ?? string.Empty;

	var items = new List<SyndicationItem>();

	foreach (var file in new DirectoryInfo($"/torrents/{folder}").GetFiles())
	{
		var title = Cleanup(file.Name);
		var link = await GetItemUri(baseUrl, folder, file.Name);
		var enclosureLink = await CreateMediaEnclosureLink(baseUrl, folder, file);
		var id = GetItemId(title);

		var item = new SyndicationItem(title, title, link, id, file.CreationTimeUtc)
		{
			PublishDate = file.CreationTimeUtc
		};
		item.Links.Add(enclosureLink);
		
		items.Add(item);
	}

	var feed = new SyndicationFeed(
		title: $"Feedarr {folder} feed",
		description: $"Feedarr {folder} feed",
		feedAlternateLink: new($"{baseUrl}/{folder}.rss"),
		items: items)
	{
		TimeToLive = TimeSpan.FromMinutes(30)
	};

	return Results.Text(FeedToByteArray(feed), "application/rss+xml; charset=utf-8");
});

app.MapGet("/{folder}/{fileName}.torrent", async (string folder, string fileName)
	=> Path.Exists($"/torrents/{folder}/{HttpUtility.UrlDecode(fileName)}.torrent")
		? Results.File(await File.ReadAllBytesAsync($"/torrents/{folder}/{HttpUtility.UrlDecode(fileName)}.torrent"))
		: Results.NotFound());

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

async Task<Uri?> GetItemUri(string baseUrl, string folder, string fileName)
{
	var fileExtension = Path.GetExtension(fileName);
	
	switch (fileExtension)
	{
		case ".torrent":
			return new($"{baseUrl}/{folder}/{HttpUtility.UrlEncode(fileName)}");
		case ".magnet":
		{
			var magnetUrl = await File.ReadAllTextAsync($"/torrents/{folder}/{fileName}");
			return new(magnetUrl);
		}
		default:
			return null;
	}
}

string GetItemId(string title)
	=> Convert.ToHexString(MD5.HashData(Encoding.Unicode.GetBytes(title))).ToLowerInvariant();

string GetMediaType(string fileExtension)
	=> fileExtension switch
	{
		".torrent" => "application/x-bittorrent",
		".magnet" => "x-scheme-handler/magnet",
		_ => string.Empty
	};
	
async Task<SyndicationLink> CreateMediaEnclosureLink(string baseUrl, string folder, FileInfo file) 
{
	var uri = await GetItemUri(baseUrl, folder, file.Name);
	var mediaType = GetMediaType(file.Extension);
	
	return SyndicationLink.CreateMediaEnclosureLink(uri, mediaType, file.Length);
}

byte[] FeedToByteArray(SyndicationFeed feed) 
{
	using var stream = new MemoryStream();
	using var xmlWriter = XmlWriter.Create(stream, new() 
	{
		Encoding = Encoding.UTF8,
		NewLineHandling = NewLineHandling.Entitize,
		NewLineOnAttributes = true,
		Indent = true,
	});
	
	new Rss20FeedFormatter(feed, false).WriteTo(xmlWriter);
	xmlWriter.Flush();
	
	return stream.ToArray();
}