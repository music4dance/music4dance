using System.Text.Json;

namespace m4d.Services;

/// <summary>
/// A message shown to every visitor in the status banner. <see cref="Persist"/> says whether it
/// survives a restart; <see cref="SetBy"/> and <see cref="SetAt"/> (UTC) are for the admin page.
/// </summary>
public record UpdateMessageState(string Message, bool Persist, string SetBy, DateTime SetAt);

/// <summary>
/// Owns the admin update message that /api/health/status hands to the status banner.
///
/// With a file manager, the file is the source of truth: every change is written to it, and
/// reads re-load it once <see cref="RefreshInterval"/> has passed, so instances sharing the
/// /home volume pick up a change made on any of them. A message that isn't persisted is dropped
/// by the first instance to start after it was set, which on a single instance is "a restart
/// clears it". Without a file manager (the sandbox) the message lives in memory only.
///
/// See architecture/infrastructure/service-resilience.md (Client status banner).
/// </summary>
public class UpdateMessageService
{
    /// <summary>
    /// How stale the in-memory copy may get before a read goes back to the file. Matches the
    /// banner's fast poll, so a change shows up within about one poll on every instance.
    /// </summary>
    public static TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    private readonly ILogger<UpdateMessageService> _logger;
    private readonly IUpdateMessageFileManager _files;
    private readonly DateTime _startedAt;
    private readonly object _lock = new();

    private UpdateMessageState _state;
    private DateTime _readAt = DateTime.MinValue;
    private bool _readFailed;

    // Set when a change couldn't be written: re-reading the file would silently undo it, so
    // this instance keeps what the admin asked for until the next change succeeds.
    private bool _pinned;

    public UpdateMessageService(
        ILogger<UpdateMessageService> logger, IUpdateMessageFileManager files, DateTime startedAt)
    {
        _logger = logger;
        _files = files;
        _startedAt = startedAt;

        if (_files == null)
        {
            return;
        }

        lock (_lock)
        {
            if (TryRead(out var stored) && stored != null && !Survives(stored))
            {
                _logger.LogInformation("Dropping non-persisted update message set at {SetAt}", stored.SetAt);
                TryWrite(null);
            }
            _readAt = DateTime.UtcNow;
        }
    }

    public UpdateMessageState Current
    {
        get
        {
            if (_files == null)
            {
                return _state;
            }

            lock (_lock)
            {
                if (!_pinned && DateTime.UtcNow - _readAt >= RefreshInterval)
                {
                    _ = TryRead(out _);
                    _readAt = DateTime.UtcNow;
                }
                return _state;
            }
        }
    }

    public string Message => Current?.Message;

    /// <summary>
    /// Shows <paramref name="message"/> (whitespace-only clears it). Returns false when it couldn't
    /// be saved, in which case it shows on this instance only and won't survive a restart.
    /// </summary>
    public bool Set(string message, bool persist, string setBy)
    {
        var state = string.IsNullOrWhiteSpace(message)
            ? null
            : new UpdateMessageState(message.Trim(), persist, setBy, DateTime.UtcNow);
        _logger.LogInformation("Update message set by {SetBy} (persist: {Persist}): {Message}",
            setBy, persist, state?.Message);

        lock (_lock)
        {
            _state = state;
            if (_files == null)
            {
                return true;
            }

            var saved = TryWrite(state);
            _pinned = !saved;
            _readAt = DateTime.UtcNow;
            return saved;
        }
    }

    public bool Clear(string setBy) => Set(null, persist: false, setBy);

    private bool Survives(UpdateMessageState state) => state.Persist || state.SetAt >= _startedAt;

    // Replaces _state with what's on disk; on failure keeps the current value and logs once
    private bool TryRead(out UpdateMessageState stored)
    {
        stored = null;
        try
        {
            var json = _files.Read();
            stored = json == null ? null : JsonSerializer.Deserialize<UpdateMessageState>(json);
            _state = stored != null && !string.IsNullOrWhiteSpace(stored.Message) && Survives(stored)
                ? stored
                : null;
            if (_readFailed)
            {
                _logger.LogInformation("Update message file readable again");
                _readFailed = false;
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            if (!_readFailed)
            {
                _logger.LogWarning(e, "Couldn't read the update message file; keeping the current message");
                _readFailed = true;
            }
            return false;
        }
    }

    private bool TryWrite(UpdateMessageState state)
    {
        try
        {
            if (state == null)
            {
                _files.Delete();
            }
            else
            {
                _files.Write(JsonSerializer.Serialize(state));
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(e, "Couldn't save the update message; it shows on this instance only");
            return false;
        }
    }
}
