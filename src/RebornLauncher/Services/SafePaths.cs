using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PokemonRebornLauncher.Services;

public static class SafePaths
{
    public static string ValidateRelative(string value)
    {
        string path = value.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || Path.IsPathRooted(path)) throw new InvalidDataException($"Unsicherer Dateipfad: {value}");
        foreach (var part in path.Split('/'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.Any(c => c < 32 || "<>:\"|?*".Contains(c)) ||
                Regex.IsMatch(part.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase))
                throw new InvalidDataException($"Unsicherer Dateipfad: {value}");
        }
        return path;
    }
    public static string Under(string root, string relative)
    {
        relative = ValidateRelative(relative);
        string target = Path.GetFullPath(Path.Combine(root, relative));
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Datei außerhalb des Spielordners.");
        NoLinks(target);
        return target;
    }
    public static void NoLinks(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Verknüpfungen im Updatepfad sind nicht erlaubt: {current}");
            current = Path.GetDirectoryName(current);
        }
    }
    public static bool IsProtected(string relative)
    {
        string path = ValidateRelative(relative);
        var parts = path.Split('/');
        var name = parts[^1];
        if (parts.Any(p => p.Equals("Save Data", StringComparison.OrdinalIgnoreCase) || p.Equals("SaveData", StringComparison.OrdinalIgnoreCase) || p.Equals("Saves", StringComparison.OrdinalIgnoreCase) || p.Equals("Saved Games", StringComparison.OrdinalIgnoreCase) || p.Equals("Screenshots", StringComparison.OrdinalIgnoreCase) || p.Equals(".reborn-updater", StringComparison.OrdinalIgnoreCase) || p.Equals(".git", StringComparison.OrdinalIgnoreCase))) return true;
        if (Regex.IsMatch(name, @"^(Game(?:_.*)?|LastSave|Save|Settings)\.(rxdata|rxdata\.bak|sav|dat)$", RegexOptions.IgnoreCase)) return true;
        if (parts.Length != 1) return false;
        return name.Equals("settings.json", StringComparison.OrdinalIgnoreCase) || name.Equals("mkxp.json", StringComparison.OrdinalIgnoreCase) || name.Equals("mkxp.conf", StringComparison.OrdinalIgnoreCase) || name.Equals("errorlog.txt", StringComparison.OrdinalIgnoreCase) || name.StartsWith("launcher.exe", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Pokemon Reborn Launcher", StringComparison.OrdinalIgnoreCase) || name.Equals(Path.GetFileName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase);
    }
    public static string Extract(string zipPath, string destination, IProgress<UpdateProgress> progress, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count == 0 || zip.Entries.Count > 200000) throw new InvalidDataException("Ungültige Anzahl an Archivdateien.");
        var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            bool directory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            string key = ValidateRelative(directory ? entry.FullName[..^1] : entry.FullName);
            int mode = (entry.ExternalAttributes >> 16) & 0xF000;
            if ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 || mode == 0xA000) throw new InvalidDataException("Archiv enthält eine Verknüpfung.");
            if (!seen.TryAdd(key, directory)) throw new InvalidDataException($"Doppelter Archivpfad: {key}");
            expanded = checked(expanded + entry.Length);
            if (expanded > 30L * 1024 * 1024 * 1024) throw new InvalidDataException("Entpacktes Update ist zu groß.");
        }
        foreach (var key in seen.Keys)
        {
            var ancestor = key;
            while (ancestor.Contains('/'))
            {
                ancestor = ancestor[..ancestor.LastIndexOf('/')];
                if (seen.TryGetValue(ancestor, out bool isDirectory) && !isDirectory) throw new InvalidDataException("Archivdatei kollidiert mit einem Ordner.");
            }
        }
        Directory.CreateDirectory(destination);
        int index = 0;
        byte[] buffer = new byte[131072];
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            bool directory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            string path = Under(destination, directory ? entry.FullName[..^1] : entry.FullName);
            if (directory) Directory.CreateDirectory(path);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var source = entry.Open();
                using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                long written = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int read = source.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    written += read;
                    if (written > entry.Length) throw new InvalidDataException("Die entpackte Datei ist größer als angekündigt.");
                    target.Write(buffer, 0, read);
                }
                if (written != entry.Length) throw new InvalidDataException("Eine Archivdatei ist unvollständig.");
            }
            progress.Report(new(UpdateStage.Extracting, ++index * 100d / zip.Entries.Count, 0, null, 0, "Spieldateien werden entpackt …"));
        }
        var dirs = Directory.GetDirectories(destination);
        return Directory.GetFiles(destination).Length == 0 && dirs.Length == 1 ? dirs[0] : destination;
    }
}
