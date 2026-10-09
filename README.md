# Feedarr

A tiny ASP.NET Core minimal API that turns a folder of `.torrent` and `.magnet`
files into RSS feeds, so tools like Sonarr, Radarr or any RSS-capable torrent
client can pick them up.

## How it works

Every sub folder of `/torrents` becomes a feed:

```
/torrents
├── movies
│   ├── Some.Movie.2024.torrent
│   └── Another.Movie.2023.magnet
└── series
    └── Some.Show.S01E01.torrent
```

| Endpoint                      | Description                                              |
| ----------------------------- | -------------------------------------------------------- |
| `GET /{folder}.rss`           | RSS 2.0 feed with one item per file in `/torrents/{folder}` |
| `GET /{folder}/{name}.torrent`| Downloads the `.torrent` file                            |
| `GET /favicon.ico`            | Served from `wwwroot`                                    |

- `.torrent` files are linked to this service, which serves the file itself.
- `.magnet` files must contain a single magnet URI, which is used directly as the item link.
- Item titles are the file name without extension, dots replaced by spaces and emoticons removed.
- Item ids are an MD5 hash of the title, so ids stay stable between requests.
- Feeds advertise a TTL of 30 minutes.

## Configuration

| Variable   | Description                                                        |
| ---------- | ------------------------------------------------------------------ |
| `BASE_URL` | Public base URL of the service, used to build links (e.g. `https://feedarr.example.com`) |

## Running

### Docker

```bash
docker run -d \
  -p 8080:8080 \
  -e BASE_URL=http://localhost:8080 \
  -v /path/to/torrents:/torrents:ro \
  feedarr
```

### Locally

```bash
dotnet run
```

On Windows or macOS the `/torrents` path is resolved from the drive root,
so create the folder there or run inside a container.

## Usage

Open `http://localhost:8080/movies.rss` in your RSS reader or add it as an
indexer/feed in your download automation tool.

## Requirements

- .NET 10 SDK or newer