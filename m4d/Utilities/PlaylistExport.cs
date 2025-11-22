using System.Diagnostics;
using System.Text;

using m4d.Services;

using m4dModels;

using Microsoft.AspNetCore.Identity;

namespace m4d.Utilities;

public class PlaylistExport(ExportInfo info, SongIndex songIndex, UserManager<ApplicationUser> userManager, IBackgroundTaskQueue queue, string spotifyId = null)
{
    private readonly SongFilter _filter = songIndex.DanceMusicService.SearchService.GetSongFilter(info.Filter);

    public async Task<byte[]> Export(string userName)
    {
        return await ExportInternal(userName, song => song.ToCsv(userName, ExportLevel));
    }

    public async Task<byte[]> ExportFilteredDances(string userName)
    {
        var danceIds = _filter.DanceQuery?.DanceIds.ToList();
        if (danceIds == null || danceIds.Count == 0)
        {
            return await Export(userName);
        }

        return await ExportInternal(userName,
            song =>
            {
                return string.Join("", danceIds.Select(id => song.CsvForDance(id, userName, ExportLevel)));
            });
    }

    private async Task<byte[]> ExportInternal(string userName, Func<Song, string> export)
    {
        var csv = new StringBuilder();
        try
        {
            var results = await FindSongs(userName, info.Count);
            csv.AppendLine(Song.GetCsvHeader(ExportLevel));

            foreach (var song in results.Songs)
            {
                csv.Append(export(song));
            }

            if (!string.IsNullOrWhiteSpace(info.Description))
            {
                csv.AppendLine();
                csv.AppendLine($"\"Exported from https://www.music4dance.net on {DateTime.Now:f} - {info.Description}\"");
                if (info.Count > 0)
                {
                    csv.AppendLine(
                        $"Created by search:,\"https://www.music4dance.net/song/filtersearch?filter={_filter.ToString()}\"");
                }
                if (spotifyId != null)
                {
                    csv.AppendLine($"Spotify Playlist:,\"https://open.spotify.com/playlist/{spotifyId}\"");
                }
            }
        }
        catch (Exception e)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceError, e.Message);
            throw new Exception(
                "Unable to create a playlist at this time.  Please report the issue.");
        }

        return Encoding.Unicode.GetBytes(csv.ToString());
    }

    private async Task<SearchResults> FindSongs(string userName, int count)
    {
        var p = songIndex.AzureParmsFromFilter(_filter, count);
        p.IncludeTotalCount = true;
        var results = await new SongSearch(
            _filter, userName, true, songIndex, userManager, queue, count).Search();

        return results;
    }

    private ExportLevel ExportLevel => info.IsPremium ? (info.Count == -1 ? ExportLevel.Global : ExportLevel.Personal) : ExportLevel.Sparse;
}
