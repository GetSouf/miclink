namespace MicLinkWinUI.Infrastructure.Audio.Effects;

using MicLinkWinUI.Domain.Models;

public static class VstPluginScanner
{
    private static readonly string[] SearchRoots =
    [
        @"C:\Program Files\Common Files\VST3",
        @"C:\Program Files\VST3",
        @"C:\Program Files\Steinberg\VstPlugins",
        @"C:\Program Files\VstPlugins",
        @"C:\Program Files\VSTPlugins",
        @"C:\Program Files\Image-Line\FL Studio\Plugins\VST",
        @"C:\Program Files\Image-Line\FL Studio\Plugins\VST3",
    ];

    public static Task<IReadOnlyList<VstPluginInfo>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(), cancellationToken);

    public static IReadOnlyList<VstPluginInfo> Scan()
    {
        var results = new List<VstPluginInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in EnumerateSearchRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                ScanVst3(root, results, seen);
                ScanVst2(root, results, seen);
            }
            catch
            {
                // Skip unreadable plugin folders.
            }
        }

        return results
            .OrderBy(static p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        foreach (var root in SearchRoots)
        {
            yield return root;
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "Programs", "Common", "VST3");

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(programFiles, "Common Files", "VST3");
        yield return Path.Combine(programFiles, "VST3");
    }

    private static void ScanVst3(string root, List<VstPluginInfo> results, HashSet<string> seen)
    {
        foreach (var path in Directory.EnumerateDirectories(root, "*.vst3", SearchOption.AllDirectories))
        {
            if (!seen.Add(path))
            {
                continue;
            }

            results.Add(new VstPluginInfo
            {
                Path = path,
                Name = Path.GetFileNameWithoutExtension(path),
                Format = "VST3",
            });
        }
    }

    private static void ScanVst2(string root, List<VstPluginInfo> results, HashSet<string> seen)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories))
        {
            if (!seen.Add(path))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            if (name.Contains(' ', StringComparison.Ordinal) ||
                name.Contains("vst3", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new VstPluginInfo
            {
                Path = path,
                Name = name,
                Format = "VST2",
            });
        }
    }
}
