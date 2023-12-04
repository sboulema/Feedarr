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
		TimeToLive = TimeSpan.FromMinutes(1),
		Description = new($"Feedarr {folder} feed")
	};

	var items = new List<SyndicationItem>();

	foreach (var file in new DirectoryInfo($"/torrents/{folder}").GetFiles())
	{
		var title = Cleanup(file.Name);
		var link = await CreateMediaEnclosureLink(baseUrl, folder, file);
		
		var item = new SyndicationItem
		{
			Title = new(title),
			PublishDate = file.CreationTimeUtc
		};
		item.Links.Add(link);
	}

	return Results.Text(FeedToString(feed), "text/xml");
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

string FeedToString(SyndicationFeed feed) 
{
	var rssFormatter = new Rss20FeedFormatter(feed, false);
	var output = new StringBuilder();
	using var writer = XmlWriter.Create(output, new XmlWriterSettings { Indent = true });
	rssFormatter.WriteTo(writer);
	writer.Flush();
	return output.ToString();
}