using System.Text;

namespace Open.FileExplorer.X
{
    internal static class StringEx
    {
        public static string Format(this string baseString, int position, string value)
        {
            var search = "{" + position.ToString() + "}";
            var builder = new StringBuilder(baseString);
            builder.Replace(search, value);
            return builder.ToString();
        }
    }
}
