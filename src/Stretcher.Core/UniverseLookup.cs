using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stretcher;

internal static class UniverseLookup
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static Task<string> ResolveAsync(string normalizedPlaceIds)
    {
        return ResolveAsync(normalizedPlaceIds, null, CancellationToken.None);
    }

    internal static async Task<string> ResolveAsync(string normalizedPlaceIds, HttpMessageHandler? handler, CancellationToken cancellation)
    {
        string[] places = (normalizedPlaceIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (places.Length == 0)
            throw new InvalidOperationException("Enter at least one Place ID or Roblox game link.");

        using HttpClient client = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Timeout;
        List<long> universes = [];
        foreach (string place in places)
        {
            string url = "https://apis.roblox.com/universes/v1/places/" + place + "/universe";
            string body;
            try
            {
                body = await client.GetStringAsync(url, cancellation);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new InvalidOperationException("Could not look up the Universe ID for place " + place + ". Check the Place ID and your connection.", ex);
            }

            long universe = ReadUniverseId(body, place);
            if (!universes.Contains(universe))
                universes.Add(universe);
        }

        return string.Join(",", universes.Select(id => id.ToString(CultureInfo.InvariantCulture)));
    }

    private static long ReadUniverseId(string body, string place)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("universeId", out JsonElement value) && value.TryGetInt64(out long id) && id > 0)
                return id;
        }
        catch (JsonException)
        {
        }

        throw new InvalidOperationException("Roblox did not return a Universe ID for place " + place + ".");
    }
}
