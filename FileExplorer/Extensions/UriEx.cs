using System;
using System.Collections.Generic;
using System.Text;

namespace Open.FileExplorer
{
    public static class UriEx
    {
        /// <summary>
        /// Process the URI fragment string.
        /// </summary>
        /// <param name="fragment">The URI fragment.</param>
        /// <returns>The key-value pairs.</returns>
        public static Dictionary<string, string> ProcessFragments(string fragment)
        {
            Dictionary<string, string> processedFragments = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(fragment))
            {
                if (fragment[0] == '#' || fragment[0] == '?')
                {
                    fragment = fragment.Substring(1);
                }

                string[] fragmentParams = fragment.Split('&');

                foreach (string fragmentParam in fragmentParams)
                {
                    int sepIndex = fragmentParam.IndexOf('=');
                    if (sepIndex >= 0)
                    {
                        var key = fragmentParam.Substring(0, sepIndex);
                        var value = fragmentParam.Substring(sepIndex + 1);
                        processedFragments.Add(key, Uri.UnescapeDataString(value));
                    }
                }
            }
            return processedFragments;
        }

        public static string EscapeUriString(string text)
        {
            var builder = new StringBuilder(Uri.EscapeDataString(text));
            builder.Replace("!", "%21");
            builder.Replace("'", "%27");
            builder.Replace("(", "%28");
            builder.Replace(")", "%29");
            return builder.ToString();
        }
    }
}
