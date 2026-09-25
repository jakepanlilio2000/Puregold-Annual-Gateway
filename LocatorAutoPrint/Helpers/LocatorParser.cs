using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LocatorAutoPrint.Helpers
{
    public static class LocatorParser
    {
        private const int MaxBatchLimit = 5000;
        private static readonly Regex ValidFormatRegex = new Regex(@"^\s*\d+\s*(,\s*\d+\s*)*$", RegexOptions.Compiled);

        public static bool IsValidFormat(string inputStr)
        {
            if (string.IsNullOrWhiteSpace(inputStr)) return false;
            if (!ValidFormatRegex.IsMatch(inputStr)) return false;

            var parts = inputStr.Split(',');
            foreach (var part in parts)
            {
                if (!int.TryParse(part.Trim(), out int val) || val <= 0)
                {
                    return false;
                }
            }
            return true;
        }

        public static List<int> Parse(string inputStr)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(inputStr)) return new List<int>();

            // Locator format is strictly digits separated by commas (e.g. 1,2,3 or 34,87,100). No other formats.
            if (!ValidFormatRegex.IsMatch(inputStr))
            {
                return new List<int>();
            }

            var parts = inputStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (int.TryParse(trimmed, out int single) && single > 0)
                {
                    result.Add(single);
                    if (result.Count >= MaxBatchLimit) break;
                }
                else
                {
                    // Invalid numeric value (e.g. zero, negative, or overflow)
                    return new List<int>();
                }
            }

            return result.OrderBy(x => x).ToList();
        }
    }
}