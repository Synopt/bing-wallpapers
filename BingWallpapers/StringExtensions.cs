using System.Text.RegularExpressions;

namespace BingWallpapers
{
    internal static class StringExtensions
    {
        public static string SplitPascalCase(this string str, bool toLower = true)
        {
            return Regex.Replace(str, "[a-z][A-Z]", m => $"{m.Value[0]} {(toLower ? char.ToLower(m.Value[1]) : m.Value[1])}");
        }
    }
}
