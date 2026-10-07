using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Stretcher;

internal static class Store
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RobloxDisplayProfile");

    // Recovery keeps the old path so a session left by an earlier release can still be restored.
    public static string LegacyConfigPath => Path.Combine(Root, "config.json");

    public static readonly string AppRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stretcher");

    public static string SessionPath => Path.Combine(Root, "session.json");

    public static string PathFor(string prefix, string? token)
    {
        return Path.Combine(Root, prefix + "." + token);
    }

    internal static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    public static T Read<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), Json)
            ?? throw new InvalidDataException("The settings file is empty or invalid.");
    }

    public static void Save(string path, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Atomic(path, JsonSerializer.Serialize(value, value.GetType(), Json));
    }

    public static void Atomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
            using (FileStream file = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes, 0, bytes.Length);
                file.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(AppRoot);
            string path = Path.Combine(AppRoot, "diagnostic.log");
            if (File.Exists(path) && new FileInfo(path).Length > 500000)
                File.Delete(path);
            File.AppendAllText(path, DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture) + " " + text + Environment.NewLine, Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
