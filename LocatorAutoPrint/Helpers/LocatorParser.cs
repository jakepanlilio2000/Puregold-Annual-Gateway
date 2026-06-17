using System.Collections.Generic;
using System.Linq;

namespace LocatorAutoPrint.Helpers
{
    public static class LocatorParser
    {
        public static List<int> Parse(string inputStr)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(inputStr)) return result.ToList();

            var parts = inputStr.Split('.');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains("-"))
                {
                    var rangeParts = trimmed.Split('-');
                    if (rangeParts.Length == 2 &&
                        int.TryParse(rangeParts[0], out int start) &&
                        int.TryParse(rangeParts[1], out int end) &&
                        start <= end)
                    {
                        for (int i = start; i <= end; i++) result.Add(i);
                    }
                }
                else if (int.TryParse(trimmed, out int single))
                {
                    result.Add(single);
                }
            }
            return result.OrderBy(x => x).ToList();
        }
    }
}