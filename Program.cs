using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
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

	var feed = new SyndicationFeed
	{
		Title = new($"Feedarr {folder} feed"),
		Description = new($"Feedarr {folder} feed"),
	};

	var items = new List<SyndicationItem>();

	foreach (var file in new DirectoryInfo($"/torrents/{folder}").GetFiles())
	{
		var title = Cleanup(file.Name);
		var link = await GetItemUri(baseUrl, folder, file.Name);
		var enclosureLink = await CreateMediaEnclosureLink(baseUrl, folder, file);
		
		var item = new SyndicationItem(title, title, link, link.ToString(), file.CreationTimeUtc);
		item.Links.Add(enclosureLink);
		
		items.Add(item);
	}
	
	feed.Items = items;

	return Results.Text(FeedToByteArray(feed), "application/rss+xml; charset=utf-8");
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

async Task<Uri?> GetItemUri(string baseUrl, string folder, string fileName)
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

	return null;
}

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
		Indent = true
	});
	
	new Rss20FeedFormatter(feed, false).WriteTo(xmlWriter);
	xmlWriter.Flush();
	
	return stream.ToArray();
}