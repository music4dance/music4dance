using System.Linq;
using System.Text.RegularExpressions;

namespace m4dModels.Utilities
{
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
    }
}
