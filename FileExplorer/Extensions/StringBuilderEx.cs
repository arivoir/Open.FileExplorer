using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Open.FileExplorer
{
    internal static class StringBuilderEx
    {
        private static char[] _trimCharacters = new char[] { ' ' };
        public static void Trim(this StringBuilder baseString)
        {
            Trim(baseString, _trimCharacters);
        }

        public static void Trim(this StringBuilder baseString, char[] trimCharacters)
        {
            while (baseString.Length > 0)
            {
                if (trimCharacters.Contains(baseString[0]))
                {
                    baseString.Remove(0, 1);
                }
                break;
            }
            while (baseString.Length > 0)
            {
                if (trimCharacters.Contains(baseString[baseString.Length - 1]))
                {
                    baseString.Remove(baseString.Length - 1, 1);
                }
                break;
            }
        }

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
