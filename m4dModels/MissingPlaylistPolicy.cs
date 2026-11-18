namespace m4dModels;

/// <summary>
/// Decides what the SongsFromSpotify update batch does with playlists Spotify answered 404 for.
/// Only a 404 counts (5xx, 429, auth and network failures are transient or ours), and a playlist
/// is only soft-deleted once it has stayed missing for <see cref="GracePeriod"/>, so one bad run
/// can't delete anything. When a large share of a run is missing at once, the cause is almost
/// certainly on our side or a Spotify-wide change, so the run changes nothing and the caller
/// raises an error for a person to look at.
/// </summary>
public static class MissingPlaylistPolicy
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    // About 800 playlists run daily and ~5% were dead on 2026-11-18, so normal churn stays
    // well under these; an outage or policy change hits most of the batch
    public const int MassFailureMinimum = 10;
    public const double MassFailureFraction = 0.1;

    public static bool IsMassFailure(int notFound, int attempted) =>
        notFound >= MassFailureMinimum && notFound >= attempted * MassFailureFraction;

    /// <summary>
    /// Applies one run's 404s: starts the clock on newly missing playlists and soft-deletes the
    /// ones missing for the whole grace period. A mass failure changes nothing.
    /// </summary>
    public static MissingPlaylistOutcome Apply(
        IReadOnlyCollection<PlayList> notFound, int attempted, DateTime now)
    {
        if (IsMassFailure(notFound.Count, attempted))
        {
            return new MissingPlaylistOutcome(true, [], []);
        }

        var started = notFound.Where(p => p.NotFoundSince == null).ToList();
        started.ForEach(p => p.NotFoundSince = now);

        var deleted = notFound.Where(p => now - p.NotFoundSince >= GracePeriod).ToList();
        deleted.ForEach(p =>
        {
            p.Deleted = true;
            p.Updated = now;
        });

        return new MissingPlaylistOutcome(false, started, deleted);
    }
}

public record MissingPlaylistOutcome(
    bool MassFailure, IReadOnlyList<PlayList> Started, IReadOnlyList<PlayList> Deleted);
