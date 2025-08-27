using System.Text;

namespace Open.FileExplorer.OneDrive
{
    internal static class StringEx
    {
        public static string Replace(this string baseString, char[] oldChars, char newChar)
        {
            var builder = new StringBuilder(baseString);
            builder.ReplaceMany(oldChars, newChar);
            return builder.ToString();
        }

        public static void ReplaceMany(this StringBuilder baseString, char[] oldChars, char newChar)
        {
            foreach (var oldChar in oldChars)
            {
                baseString.Replace(oldChar, newChar);
            }
        }
        
        public static string Format(this string baseString, int position, string value)
        {
            var search = "{" + position.ToString() + "}";
            var builder = new StringBuilder(baseString);
            builder.Replace(search, value);
            return builder.ToString();
        }
    }
}
