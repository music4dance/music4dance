using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace m4dModels.Utilities
{
    public static class StringHelpers
    {
        // Trim whitespace from beginning and end and collate internal whitespace into single spaces
        public static string CleanWhitespace(this string str)
        {
            return SingleSpacify.Replace(str.Trim(), " ");
        }

        private static readonly Regex SingleSpacify = new Regex("\\s{2,}", RegexOptions.Compiled);

        public static string CleanFilename(this string filename) {
            return new string(filename.Except(System.IO.Path.GetInvalidFileNameChars()).ToArray());
        }
    }
}
