using System.Diagnostics;

namespace Scrinium.Desktop.Services;

public static class ExternalOpener
{
    public static void Open(string path)
    {
        var info = OperatingSystem.IsMacOS() ? new ProcessStartInfo("open", [path])
            : OperatingSystem.IsLinux() ? new ProcessStartInfo("xdg-open", [path])
            : new ProcessStartInfo(path) { UseShellExecute = true };

        Process.Start(info);
    }
}
