using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

namespace Feedarr.Helpers;

/// <summary>
/// Helper methods for building torrent RSS feeds.
/// </summary>
internal static class FeedHelper
{
    /// <summary>
    /// Root directory that contains one sub folder per feed.
    /// </summary>
    public const string TorrentsRoot = "/torrents";

    /// <summary>
    /// Turns a file name into a human readable feed item title by removing the
    /// extension, replacing dots with spaces and stripping emoticons.
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
    /// Gets the link for a feed item. For <c>.torrent</c> files this is a URL to this
    /// service, for <c>.magnet</c> files it is the magnet URI stored in the file.
    /// </summary>
    /// <param name="baseUrl">Public base URL of this service.</param>
    /// <param name="folder">Name of the feed folder.</param>
    /// <param name="fileName">Name of the file, including extension.</param>
    /// <returns>The item URI, or <c>null</c> when the file type is not supported.</returns>
    public static async Task<Uri?> GetItemUri(string baseUrl, string folder, string fileName)
    {
        switch (Path.GetExtension(fileName))
        {
            case ".torrent":
                return new($"{baseUrl}/{folder}/{HttpUtility.UrlEncode(fileName)}");
            case ".magnet":
                {
                    var magnetUrl = await File.ReadAllTextAsync($"{TorrentsRoot}/{folder}/{fileName}");
                    return new(magnetUrl);
                }
            default:
                return null;
        }
    }

    /// <summary>
    /// Creates a stable, unique feed item id by hashing the title with MD5.
    /// </summary>
    /// <param name="title">The cleaned up item title.</param>
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
    /// Creates the RSS enclosure link for a torrent or magnet file.
    /// </summary>
    /// <param name="baseUrl">Public base URL of this service.</param>
    /// <param name="folder">Name of the feed folder.</param>
    /// <param name="file">The file the enclosure describes.</param>
    /// <returns>A media enclosure link with the file's media type and size.</returns>
    public static async Task<SyndicationLink> CreateMediaEnclosureLink(string baseUrl, string folder, FileInfo file)
    {
        var uri = await GetItemUri(baseUrl, folder, file.Name);

        return SyndicationLink.CreateMediaEnclosureLink(uri, GetMediaType(file.Extension), file.Length);
    }

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
