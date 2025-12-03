using System.ComponentModel.DataAnnotations;

namespace m4dModels;

/// <summary>
/// Request model for adding a song to a Spotify playlist.
/// </summary>
public class AddToPlaylistRequest
{
    /// <summary>
    /// The m4d song identifier
    /// </summary>
    [Required(ErrorMessage = "SongId is required")]
    public string SongId { get; set; }

    /// <summary>
    /// The Spotify playlist identifier
    /// </summary>
    [Required(ErrorMessage = "PlaylistId is required")]
    public string PlaylistId { get; set; }
}
