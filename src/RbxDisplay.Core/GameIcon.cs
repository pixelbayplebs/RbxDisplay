using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RbxDisplay;

internal static class GameIcon
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static string PathFor(string gameId)
    {
        return Path.Combine(Store.AppRoot, "icons", gameId + ".png");
    }

    public static void Forget(string gameId)
    {
        try
        {
            string path = PathFor(gameId);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Store.Log("Removing game icon for " + gameId + ": " + ex.Message);
        }
    }

    public static async Task<bool> TryEnsureAsync(string gameId, string? universeIds)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameId) || File.Exists(PathFor(gameId)))
                return File.Exists(PathFor(gameId));
            if (!long.TryParse(FirstUniverse(universeIds), NumberStyles.None, CultureInfo.InvariantCulture, out long universe) || universe <= 0)
                return false;
            string? url = await LookupAsync(universe, null, CancellationToken.None);
            if (url == null)
                return false;
            await DownloadAsync(url, PathFor(gameId), null, CancellationToken.None);
            return File.Exists(PathFor(gameId));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Store.Log("Game icon for " + gameId + ": " + ex.Message);
            return false;
        }
    }

    internal static string? ReadImageUrl(string body, long universeId)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
                return null;
            foreach (JsonElement item in data.EnumerateArray())
            {
                if (item.TryGetProperty("targetId", out JsonElement target) && target.TryGetInt64(out long id) && id != universeId)
                    continue;
                if (!item.TryGetProperty("state", out JsonElement state) || !string.Equals(state.GetString(), "Completed", StringComparison.OrdinalIgnoreCase))
                    return null;
                if (item.TryGetProperty("imageUrl", out JsonElement image) && image.ValueKind == JsonValueKind.String)
                {
                    string? url = image.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                        return url;
                }

                return null;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string? FirstUniverse(string? universeIds)
    {
        if (string.IsNullOrWhiteSpace(universeIds))
            return null;
        string[] parts = universeIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts[0];
    }

    private static async Task<string?> LookupAsync(long universe, HttpMessageHandler? handler, CancellationToken cancellation)
    {
        using HttpClient client = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Timeout;
        string address = "https://thumbnails.roblox.com/v1/games/icons?universeIds=" + universe.ToString(CultureInfo.InvariantCulture) + "&returnPolicy=PlaceHolder&size=150x150&format=Png&isCircular=false";
        string body = await client.GetStringAsync(address, cancellation);
        return ReadImageUrl(body, universe);
    }

    private static async Task DownloadAsync(string url, string path, HttpMessageHandler? handler, CancellationToken cancellation)
    {
        using HttpClient client = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Timeout;
        byte[] bytes = await client.GetByteArrayAsync(url, cancellation);
        if (bytes.Length == 0)
            throw new InvalidOperationException("Roblox returned an empty game icon.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellation);
        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }
}
