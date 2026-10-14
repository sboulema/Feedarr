using System.ServiceModel.Syndication;

namespace Feedarr.Models;

/// <summary>
/// A single torrent or magnet file, ready to be rendered as a feed item or a list entry.
/// </summary>
/// <param name="Title">The cleaned up title.</param>
/// <param name="Id">Stable unique id derived from the title.</param>
/// <param name="Link">Link to the torrent file or the magnet URI.</param>
/// <param name="Enclosure">The RSS enclosure link for the file.</param>
/// <param name="Created">Creation time of the file in UTC.</param>
internal sealed record FeedEntry(
    string Title,
    string Id,
    Uri Link,
    SyndicationLink Enclosure,
    DateTimeOffset Created);
