using Feedarr.Models;
using System.Drawing;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;

namespace Feedarr.Helpers;

/// <summary>
/// Helper methods for building torrent RSS feeds and HTML listings.
/// </summary>
internal static class FeedHelper
{
    /// <summary>
    /// Root directory that contains one sub folder per feed.
    /// </summary>
    public const string TorrentsRoot = "/torrents";

    private static readonly string[] SupportedExtensions = [".torrent", ".magnet"];

    /// <summary>
    /// Resolves a feed folder and makes sure it is a direct child of <see cref="TorrentsRoot"/>.
    /// </summary>
    /// <param name="folder">The folder name from the request.</param>
    /// <returns>The directory, or <c>null</c> when it does not exist or escapes the root.</returns>
    public static DirectoryInfo? GetFolder(string folder)
    {
        var root = Path.GetFullPath(TorrentsRoot);
        var path = Path.GetFullPath(Path.Combine(root, folder));

        return Path.GetDirectoryName(path) == root && Directory.Exists(path)
            ? new DirectoryInfo(path)
            : null;
    }

    /// <summary>
    /// Gets all supported files in a folder as entries, newest first.
    /// </summary>
    /// <param name="baseUrl">Public base URL of this service.</param>
    /// <param name="folder">Name of the feed folder.</param>
    /// <param name="directory">The resolved directory of the folder.</param>
    /// <returns>The entries ordered by creation time, descending.</returns>
    public static async Task<IReadOnlyList<FeedEntry>> GetEntries(string baseUrl, string folder, DirectoryInfo directory)
        => await Task.WhenAll(
            directory
                .GetFiles()
                .Where(file => SupportedExtensions.Contains(file.Extension))
                .OrderByDescending(file => file.CreationTimeUtc)
                .Select(async file =>
                {
                    var title = Cleanup(file.Name);
                    var link = (await GetItemUri(baseUrl, folder, file.Name))!;

                    return new FeedEntry(
                        title,
                        GetItemId(title),
                        link,
                        SyndicationLink.CreateMediaEnclosureLink(link, GetMediaType(file.Extension), file.Length),
                        file.CreationTimeUtc);
                }));

    /// <summary>
    /// Builds an RSS feed from a list of entries.
    /// </summary>
    /// <param name="baseUrl">Public base URL of this service.</param>
    /// <param name="folder">Name of the feed folder.</param>
    /// <param name="entries">The entries to include.</param>
    /// <returns>The feed.</returns>
    public static SyndicationFeed BuildFeed(string baseUrl, string folder, IReadOnlyList<FeedEntry> entries)
        => new(
            title: $"Feedarr {folder} feed",
            description: $"Feedarr {folder} feed",
            feedAlternateLink: new($"{baseUrl}/{folder}.rss"),
            items: entries.Select(entry => new SyndicationItem(entry.Title, entry.Title, entry.Link, entry.Id, entry.Created)
            {
                PublishDate = entry.Created,
                Links = { entry.Enclosure }
            }))
        {
            ImageUrl = new Uri($"{baseUrl}/favicon-512.png"),
            TimeToLive = TimeSpan.FromMinutes(30),
            LastUpdatedTime = entries
                .Select(entry => entry.Created)
                .DefaultIfEmpty(DateTimeOffset.UtcNow)
                .Max()
        };

    /// <summary>
    /// Renders an HTML page, styled with Bulma and bulma-list, with a link and creation date for every entry.
    /// </summary>
    /// <param name="baseUrl">Public base URL of this service.</param>
    /// <param name="folder">Name of the feed folder.</param>
    /// <param name="entries">The entries to list.</param>
    /// <returns>The HTML document.</returns>
    public static string RenderHtml(string baseUrl, string folder, IReadOnlyList<FeedEntry> entries)
    {
        var rssUrl = HttpUtility.HtmlAttributeEncode($"{baseUrl}/{folder}.rss");

        var listItems = string.Join(
            Environment.NewLine,
            entries.Select(entry =>
            {
                var link = HttpUtility.HtmlAttributeEncode(entry.Link.ToString());
                var label = entry.Link.Scheme == "magnet" ? "Magnet" : "Torrent";

                return $"""
				<div class="list-item">
					<div class="list-item-content">
						<div class="list-item-title">
							<a href="{link}">{HttpUtility.HtmlEncode(entry.Title)}</a>
						</div>
						<div class="list-item-description">
							<time datetime="{entry.Created:O}">{entry.Created:yyyy-MM-dd HH:mm}</time>
						</div>
					</div>
					<div class="list-item-controls">
						<div class="buttons is-right">
							<a class="button is-small" href="{link}">{label}</a>
						</div>
					</div>
				</div>
				""";
            }));

        var content = entries.Count == 0
            ? "<p class=\"has-text-grey\">No items yet.</p>"
            : $"""
			<div class="list has-hoverable-list-items has-visible-pointer-controls has-overflow-ellipsis">
			{listItems}
			</div>
			""";

        return $"""
		<!DOCTYPE html>
		<html lang="en">
		<head>
			<meta charset="utf-8">
			<meta name="viewport" content="width=device-width, initial-scale=1">
			<title>Feedarr {HttpUtility.HtmlEncode(folder)}</title>
			<link rel="icon" href="/favicon.ico" sizes="any">
			<link rel="icon" href="/favicon.svg" type="image/svg+xml">
			<link rel="apple-touch-icon" href="/apple-touch-icon.png">
			<link rel="alternate" type="application/rss+xml" href="{rssUrl}">
			<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bulma@1.0.4/css/bulma.min.css">
			<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bulma-list@3.0.0/css/bulma-list.css">
		</head>
		<body>
			<section class="section">
				<div class="container is-max-tablet">
					<div class="level is-mobile">
						<div class="level-left">
							<h1 class="title">{HttpUtility.HtmlEncode(folder)}</h1>
						</div>
						<div class="level-right">
							<a class="button is-warning is-light" href="{rssUrl}">RSS feed</a>
						</div>
					</div>
		{content}
				</div>
			</section>
		</body>
		</html>
		""";
    }

    /// <summary>
    /// Turns a file name into a human readable title by removing the extension,
    /// replacing dots with spaces and stripping emoticons.
    /// </summary>
    /// <param name="fileName">The file name, including extension.</param>
    /// <returns>The cleaned up title.</returns>
    public static string Cleanup(string fileName)
    {
        fileName = Path.GetFileNameWithoutExtension(fileName);
        fileName = fileName.Replace(".", " ");
        // Remove emoticons from file name
        fileName = Regex.Replace(fileName, @"\p{Cs}|\p{So}", string.Empty);

        return fileName.Trim();
    }

    /// <summary>
    /// Gets the link for a file. For <c>.torrent</c> files this is a URL to this
    /// service, for <c>.magnet</c> files it is the magnet URI stored in the file.
    /// </summary>
    /// <param name="baseUrl">Public base URL of this service.</param>
    /// <param name="folder">Name of the feed folder.</param>
    /// <param name="fileName">Name of the file, including extension.</param>
    /// <returns>The URI, or <c>null</c> when the file type is not supported.</returns>
    public static async Task<Uri?> GetItemUri(string baseUrl, string folder, string fileName)
    {
        switch (Path.GetExtension(fileName))
        {
            case ".torrent":
                return new($"{baseUrl}/{folder}/{HttpUtility.UrlEncode(fileName)}");
            case ".magnet":
                {
                    var magnetUrl = (await File.ReadAllTextAsync($"{TorrentsRoot}/{folder}/{fileName}")).Trim();
                    return new(magnetUrl);
                }
            default:
                return null;
        }
    }

    /// <summary>
    /// Creates a stable, unique id by hashing the title with MD5.
    /// </summary>
    /// <param name="title">The cleaned up title.</param>
    /// <returns>A lowercase hexadecimal hash.</returns>
    public static string GetItemId(string title)
        => Convert.ToHexString(MD5.HashData(Encoding.Unicode.GetBytes(title))).ToLowerInvariant();

    /// <summary>
    /// Maps a file extension to the media type used in the enclosure.
    /// </summary>
    /// <param name="fileExtension">Extension including the leading dot.</param>
    /// <returns>The media type, or an empty string for unknown extensions.</returns>
    public static string GetMediaType(string fileExtension)
        => fileExtension switch
        {
            ".torrent" => "application/x-bittorrent",
            ".magnet" => "x-scheme-handler/magnet",
            _ => string.Empty
        };

    /// <summary>
    /// Serializes a feed to indented RSS 2.0 XML encoded as UTF-8.
    /// </summary>
    /// <param name="feed">The feed to serialize.</param>
    /// <returns>The XML document as bytes.</returns>
    public static byte[] FeedToByteArray(SyndicationFeed feed)
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
}
