using System.Text.RegularExpressions;

namespace m4dModels.Utilities;

public static partial class StringHelpers
{
    // Trim whitespace from beginning and end and collate internal whitespace into single spaces
    public static string CleanWhitespace(this string str)
    {
        return SingleSpacify().Replace(str.Trim(), " ");
    }

    [GeneratedRegex("\\s{2,}", RegexOptions.Compiled)]
    private static partial Regex SingleSpacify();

    public static string CleanFilename(this string filename)
    {
        return new string([.. filename.Except(System.IO.Path.GetInvalidFileNameChars())]);
    }

    /// <summary>
    /// Unquotes a single CSV/TSV cell using RFC 4180 rules:
    /// if the value is wrapped in a matching pair of double-quotes, strip the outer
    /// quotes and unescape any internal doubled double-quotes (<c>""</c> → <c>"</c>).
    /// Values that are not wrapped in quotes are returned unchanged.
    /// </summary>
    public static string UnquoteCsvCell(this string cell)
    {
        if (cell.Length >= 2 && cell[0] == '"' && cell[^1] == '"')
        {
            return cell[1..^1].Replace("\"\"", "\"");
        }
        return cell;
    }
}
