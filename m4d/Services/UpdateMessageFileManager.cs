using System.Text;

namespace m4d.Services;

public interface IUpdateMessageFileManager
{
    /// <summary>The stored JSON, or null when there is no file.</summary>
    string Read();
    void Write(string json);
    void Delete();
}

/// <summary>
/// Stores the admin update message beside dance-environment.json. On App Service that directory
/// is on the /home volume, which survives restarts and is shared by every instance in the plan.
/// See architecture/infrastructure/service-resilience.md (Client status banner).
/// </summary>
public class UpdateMessageFileManager(string appRoot, string fileName = "update-message")
    : IUpdateMessageFileManager
{
    private string AppData => Path.Combine(appRoot, "AppData");
    private string FilePath => Path.Combine(AppData, $"{fileName}.json");

    public string Read() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;

    public void Write(string json)
    {
        _ = Directory.CreateDirectory(AppData);

        // Written aside and moved into place, like ArtistIndexFileManager, so an instance
        // re-reading the shared file never sees it half-written
        var temporary = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, json, Encoding.UTF8);
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException)
            {
                // A leftover temp file is harmless; the original failure is the one worth reporting.
            }
            catch (UnauthorizedAccessException)
            {
            }
            throw;
        }
    }

    public void Delete()
    {
        // File.Delete tolerates a missing file but not a missing directory
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}
