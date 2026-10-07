using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stretcher;

public static partial class PlaceInput
{
    [GeneratedRegex(@"^/games/([0-9]+)(?:/|$)", RegexOptions.CultureInvariant)]
    private static partial Regex GamePath();

    public static string Normalize(string? input)
    {
        List<long> ids = [];
        foreach (string part in (input ?? "").Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string digits = part;
            if (Uri.TryCreate(part, UriKind.Absolute, out Uri? uri) && (uri.Scheme == "https" || uri.Scheme == "http"))
            {
                if (!string.Equals(uri.Host, "roblox.com", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(uri.Host, "www.roblox.com", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Use a Roblox game link or a numeric Place ID.");
                Match match = GamePath().Match(uri.AbsolutePath);
                if (!match.Success)
                    throw new InvalidOperationException("Use a Roblox link containing /games/ followed by the Place ID.");
                digits = match.Groups[1].Value;
            }

            if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0)
                throw new InvalidOperationException("Enter a positive Place ID or a Roblox game link. Separate additional places with commas.");
            if (!ids.Contains(id))
                ids.Add(id);
        }

        if (ids.Count == 0)
            throw new InvalidOperationException("Enter at least one Place ID or Roblox game link.");
        return string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
    }
}
