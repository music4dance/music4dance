using m4d.Services;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics;
using System.Text;

namespace m4d.Utilities;

public class PlaylistExport
{
    private readonly ExportInfo _info;
    private readonly SongFilter _filter;
    private readonly SongIndex _songIndex;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBackgroundTaskQueue _queue;
    private readonly string _spotifyId;

    public PlaylistExport(ExportInfo info, SongIndex songIndex, UserManager<ApplicationUser> userManager, IBackgroundTaskQueue queue, string spotifyId = null)
    {
        _info = info;
        _songIndex = songIndex;
        _userManager = userManager;
        _queue = queue;
        _filter = new SongFilter(info.Filter);
        _spotifyId = spotifyId;
    }

    public async Task<byte[]> Export(string userName)
    {
        return await ExportInternal(userName, song => song.ToCsv(userName, ExportLevel));
    }

    public async Task<byte[]> ExportFilteredDances(string userName)
    {
        var danceIds = _filter.DanceQuery?.DanceIds.ToList();
        if (danceIds == null || !danceIds.Any())
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
            var results = await FindSongs(userName, _info.Count);
            csv.AppendLine(Song.GetCsvHeader(ExportLevel));

            foreach (var song in results.Songs)
            {
                csv.Append(export(song));
            }

            if (!string.IsNullOrWhiteSpace(_info.Description))
            {
                csv.AppendLine();
                csv.AppendLine($"\"Exported from https://www.music4dance.net on {DateTime.Now:f} - {_info.Description}\"");
                if (_info.Count > 0)
                {
                    csv.AppendLine(
                        $"Created by search:,\"https://www.music4dance.net/song/filtersearch?filter={_filter.ToString()}\"");
                }
                if (_spotifyId != null)
                {
                    csv.AppendLine($"Spotify Playlist:,\"https://open.spotify.com/playlist/{_spotifyId}\"");
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
        var p = _songIndex.AzureParmsFromFilter(_filter, count);
        p.IncludeTotalCount = true;
        var results = await new SongSearch(
            _filter, userName, true, _songIndex, _userManager, _queue, count).Search();

        return results;
    }

    private ExportLevel ExportLevel => _info.IsPremium ? (_info.Count == -1 ? ExportLevel.Global : ExportLevel.Personal) : ExportLevel.Sparse;
}
