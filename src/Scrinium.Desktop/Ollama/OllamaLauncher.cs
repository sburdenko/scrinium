using System.Diagnostics;

namespace Scrinium.Desktop.Ollama;

public static class OllamaLauncher
{
    private static readonly string[] BinaryCandidates =
        ["/usr/local/bin/ollama", "/opt/homebrew/bin/ollama", "/usr/bin/ollama", "ollama"];

    public static bool TryStart()
    {
        if (OperatingSystem.IsMacOS() && Directory.Exists("/Applications/Ollama.app"))
            return Launch("open", "-a Ollama");

        foreach (var binary in BinaryCandidates)
        {
            if (Path.IsPathRooted(binary) && !File.Exists(binary))
                continue;
            if (Launch(binary, "serve"))
                return true;
        }

        return false;
    }

    private static bool Launch(string fileName, string arguments)
    {
        try
        {
            var info = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            return Process.Start(info) is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
