using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RbxDisplay;

internal static class ResourceSetup
{
    private static readonly HashSet<string> IndexedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pri", ".png", ".xbf", ".xaml", ".html"
    };

    // A normal Windows publish produces resources.pri at build time. The portable
    // cross-build runs the same Microsoft SDK compiler once on Windows, before XAML
    // or display control starts. A failed setup never enters a profile session.
    public static void Ensure()
    {
        string root = AppContext.BaseDirectory;
        string marker = Path.Combine(root, "RbxDisplay.resources.pending");
        if (!File.Exists(marker))
            return;
        string compiler = Path.Combine(root, "Tools", "ResourceCompiler", "makepri.exe");
        if (!File.Exists(compiler))
            throw new InvalidOperationException("Application resources are incomplete. Extract the whole RbxDisplay folder again.");
        string temp = Path.Combine(Path.GetTempPath(), "RbxDisplay-resources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            string layout = Path.Combine(temp, "layout");
            string config = Path.Combine(temp, "priconfig.xml");
            string output = Path.Combine(temp, "resources.pri");
            Directory.CreateDirectory(layout);
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, file);
                if (relative.StartsWith("Tools" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("src" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("tests" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("artifacts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(file).Equals("resources.pri", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!IndexedExtensions.Contains(Path.GetExtension(file)))
                    continue;
                string destination = Path.Combine(layout, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }

            Run(compiler, temp, "createconfig", "/cf", config, "/dq", "en-US", "/pv", "10.0", "/o");
            XDocument document = XDocument.Load(config);
            foreach (XElement packaging in document.Descendants().Where(element => element.Name.LocalName == "packaging").ToList())
                packaging.Remove();
            XElement index = document.Descendants().First(element => element.Name.LocalName == "index");
            if (!index.Elements().Any(element => element.Name.LocalName == "indexer-config" &&
                string.Equals((string?)element.Attribute("type"), "PRI", StringComparison.OrdinalIgnoreCase)))
                index.Add(new XElement("indexer-config", new XAttribute("type", "PRI")));
            document.Save(config);
            Run(compiler, temp, "new", "/pr", layout, "/cf", config, "/in", "RbxDisplay", "/of", output, "/o");
            if (!File.Exists(output) || new FileInfo(output).Length == 0)
                throw new IOException("Windows did not produce the application resource index.");
            string destinationPri = Path.Combine(root, "resources.pri");
            string staging = destinationPri + ".tmp";
            File.Copy(output, staging, true);
            File.Move(staging, destinationPri, true);
            File.Delete(marker);
            Store.Log("Windows application resources prepared.");
        }
        catch (Exception ex)
        {
            Store.Log("Preparing application resources: " + ex);
            throw new InvalidOperationException("RbxDisplay could not prepare its application resources. Extract the full folder to a location you can write to, such as Downloads.\n" + ex.Message, ex);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void Run(string compiler, string workingDirectory, params string[] args)
    {
        ProcessStartInfo start = new(compiler)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new IOException("The Windows resource compiler could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(true);
            throw new IOException("Preparing application resources timed out.");
        }

        Task.WaitAll(output, error);
        if (process.ExitCode != 0)
            throw new IOException("The Windows resource compiler failed (" + process.ExitCode + "). " + output.Result + error.Result);
    }
}
