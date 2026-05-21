namespace m4dModels;

/// <summary>
/// Represents a block of song properties grouped by an action command (.Create, .Edit, .Delete, .Merge, etc.).
/// A block contains the action command, header metadata (User, Time), and all subsequent properties
/// until the next action command.
/// </summary>
public class SongPropertyBlock
{
    public string ActionCommand { get; set; }
    public string ActionValue { get; set; }
    public string User { get; set; }
    public DateTime? Timestamp { get; set; }
    public List<SongProperty> Properties { get; set; } = [];

    /// <summary>
    /// Gets all properties including the action command and header.
    /// </summary>
    public IEnumerable<SongProperty> AllProperties
    {
        get
        {
            // Action command
            if (!string.IsNullOrEmpty(ActionCommand))
            {
                yield return new SongProperty(ActionCommand, ActionValue ?? string.Empty);
            }

            // Remaining properties (includes User, Time, and content properties)
            foreach (var prop in Properties)
            {
                yield return prop;
            }
        }
    }

    /// <summary>
    /// Checks if this block has a valid header structure.
    /// Valid headers have: Action, then User and Time in either order, then content properties.
    /// </summary>
    public bool HasValidHeader()
    {
        if (Properties.Count < 2)
        {
            return false;
        }

        var hasUser = Properties[0].Name == Song.UserField || Properties[1].Name == Song.UserField;
        var hasTime = Properties[0].Name == Song.TimeField || Properties[1].Name == Song.TimeField;

        return hasUser && hasTime;
    }
}

/// <summary>
/// Parser for splitting song properties into blocks based on action commands.
/// Handles both CUT (Create-User-Time) and CTU (Create-Time-User) order.
/// </summary>
public static class SongPropertyBlockParser
{
    /// <summary>
    /// Parses song properties into blocks, splitting on action commands (.Create, .Edit, etc.).
    /// </summary>
    /// <param name="properties">Song properties to parse</param>
    /// <param name="actionFilter">Optional filter to include only specific action commands (e.g., only .Create and .Edit)</param>
    /// <returns>List of property blocks</returns>
    public static List<SongPropertyBlock> ParseBlocks(
        IEnumerable<SongProperty> properties,
        Func<string, bool> actionFilter = null)
    {
        var blocks = new List<SongPropertyBlock>();
        SongPropertyBlock currentBlock = null;

        foreach (var prop in properties)
        {
            // Check if this property is an action command
            if (prop.IsAction)
            {
                // If we have a filter and this action doesn't match, skip it as a boundary
                var isFilteredAction = actionFilter?.Invoke(prop.Name) ?? true;

                if (isFilteredAction)
                {
                    // Flush current block if exists
                    if (currentBlock != null)
                    {
                        blocks.Add(currentBlock);
                    }

                    // Start new block
                    currentBlock = new SongPropertyBlock
                    {
                        ActionCommand = prop.Name,
                        ActionValue = prop.Value
                    };
                    continue;
                }
            }

            // Add property to current block (or skip if no block yet)
            if (currentBlock != null)
            {
                currentBlock.Properties.Add(prop);

                // Extract metadata from header
                if (prop.Name == Song.UserField && currentBlock.User == null)
                {
                    currentBlock.User = prop.Value;
                }
                else if (prop.Name == Song.TimeField && currentBlock.Timestamp == null)
                {
                    if (DateTime.TryParse(prop.Value, out var timestamp))
                    {
                        currentBlock.Timestamp = timestamp;
                    }
                }
            }
        }

        // Add final block
        if (currentBlock != null)
        {
            blocks.Add(currentBlock);
        }

        return blocks;
    }

    /// <summary>
    /// Parses blocks and sorts them chronologically by timestamp.
    /// Blocks without timestamps are placed at the beginning (DateTime.MinValue).
    /// </summary>
    public static List<SongPropertyBlock> ParseAndSortBlocks(
        IEnumerable<SongProperty> properties,
        Func<string, bool> actionFilter = null)
    {
        var blocks = ParseBlocks(properties, actionFilter);
        return blocks.OrderBy(b => b.Timestamp ?? DateTime.MinValue).ToList();
    }

    /// <summary>
    /// Flattens blocks back into a linear list of properties.
    /// </summary>
    public static List<SongProperty> FlattenBlocks(IEnumerable<SongPropertyBlock> blocks)
    {
        return blocks.SelectMany(b => b.AllProperties).ToList();
    }

    /// <summary>
    /// Creates an action filter for only .Create and .Edit commands (used by SimpleMergeSongs).
    /// </summary>
    public static Func<string, bool> CreateEditFilter()
    {
        return actionName => actionName == Song.CreateCommand || actionName == Song.EditCommand;
    }

    /// <summary>
    /// Creates an action filter that accepts all action commands (used by ChunkedSong).
    /// </summary>
    public static Func<string, bool> AllActionsFilter()
    {
        return _ => true;
    }
}
