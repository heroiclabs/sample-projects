// Copyright 2025 The Nakama Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;

namespace HiroChallenges
{
    public static class UsernameAutocomplete
    {
        /// <summary>
        /// Autocompletes comma-separated usernames. Only the last segment is autocompleted,
        /// and already-selected usernames are excluded from matches.
        /// </summary>
        /// <param name="input">The full input (may contain commas)</param>
        /// <param name="usernames">Available usernames to match against</param>
        /// <param name="comparison">String comparison type (default: OrdinalIgnoreCase)</param>
        /// <returns>The input with the last segment autocompleted</returns>
        public static string Complete(string input, IEnumerable<string> usernames,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (usernames == null)
                return input ?? string.Empty;

            input = input ?? string.Empty;

            // Split by comma and track already-selected usernames
            var lastCommaIndex = input.LastIndexOf(',');
            string prefix;
            string currentSegment;
            string whitespaceAfterComma;

            if (lastCommaIndex >= 0)
            {
                var afterComma = input.Substring(lastCommaIndex + 1);
                var trimmed = afterComma.TrimStart();
                whitespaceAfterComma = afterComma.Substring(0, afterComma.Length - trimmed.Length);
                prefix = input.Substring(0, lastCommaIndex + 1) + whitespaceAfterComma;
                currentSegment = trimmed;
            }
            else
            {
                prefix = string.Empty;
                currentSegment = input;
            }

            // If current segment is empty, match all non-excluded usernames
            var matchAll = string.IsNullOrEmpty(currentSegment);

            // Build set of already-selected usernames to exclude
            var excluded = new HashSet<string>(
                comparison == StringComparison.OrdinalIgnoreCase ||
                comparison == StringComparison.CurrentCultureIgnoreCase ||
                comparison == StringComparison.InvariantCultureIgnoreCase
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);

            if (lastCommaIndex >= 0)
            {
                var previousSegments = input.Substring(0, lastCommaIndex).Split(',');
                foreach (var segment in previousSegments)
                {
                    var trimmed = segment.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        excluded.Add(trimmed);
                }
            }

            // Find matches excluding already-selected
            var matches = new List<string>();
            foreach (var username in usernames)
            {
                if (!string.IsNullOrEmpty(username) &&
                    (matchAll || username.StartsWith(currentSegment, comparison)) &&
                    !excluded.Contains(username))
                {
                    matches.Add(username);
                }
            }

            if (matches.Count == 0)
                return input;

            if (matches.Count == 1)
                return prefix + matches[0];

            var commonPrefix = FindLongestCommonPrefix(matches, comparison);

            // If no common prefix or input already equals the common prefix, pick the first match
            if (string.IsNullOrEmpty(commonPrefix) || string.Equals(currentSegment, commonPrefix, comparison))
                return prefix + matches[0];

            return prefix + commonPrefix;
        }

        private static string FindLongestCommonPrefix(List<string> strings, StringComparison comparison)
        {
            if (strings.Count == 0)
                return string.Empty;

            var first = strings[0];
            var prefixLength = first.Length;

            for (var i = 1; i < strings.Count; i++)
            {
                prefixLength = Math.Min(prefixLength, strings[i].Length);

                for (var j = 0; j < prefixLength; j++)
                {
                    if (!CharEquals(first[j], strings[i][j], comparison))
                    {
                        prefixLength = j;
                        break;
                    }
                }
            }

            return first.Substring(0, prefixLength);
        }

        private static bool CharEquals(char a, char b, StringComparison comparison)
        {
            if (comparison == StringComparison.OrdinalIgnoreCase ||
                comparison == StringComparison.CurrentCultureIgnoreCase ||
                comparison == StringComparison.InvariantCultureIgnoreCase)
            {
                return char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
            }

            return a == b;
        }
    }
}
