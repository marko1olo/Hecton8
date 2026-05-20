using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only parser for legacy JSON localization assets used by validation and key-generation tools.
    /// Runtime localization authority is the Babel binary/hash path.
    /// </summary>
    internal static class LocalizationEditorJsonTableParser
    {
        private static readonly Regex FlatJsonEntryRegex = new Regex(
            "\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static Dictionary<string, string> ParseFlatJsonTable(string json)
        {
            var result = new Dictionary<string, string>(128);
            if (string.IsNullOrWhiteSpace(json))
                return result;

            try
            {
                MatchCollection matches = FlatJsonEntryRegex.Matches(json);
                for (int i = 0; i < matches.Count; i++)
                {
                    Match match = matches[i];
                    if (!match.Success)
                        continue;

                    string key = Regex.Unescape(match.Groups["key"].Value);
                    string value = Regex.Unescape(match.Groups["value"].Value);

                    if (!string.IsNullOrWhiteSpace(key))
                        result[key] = value;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LocalizationEditorJsonTableParser] JSON parse error: {exception.Message}");
            }

            return result;
        }
    }
}
