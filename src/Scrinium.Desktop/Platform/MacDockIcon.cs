using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Scrinium.Desktop.Platform;

[SupportedOSPlatform("macos")]
internal static class MacDockIcon
{
    private const string ObjCRuntime = "/usr/lib/libobjc.A.dylib";

    // macOS reads the Dock icon from a bundle's Info.plist; a process started by `dotnet run` or an IDE has none,
    // and Window.Icon does not change it.
    public static bool RunsInsideBundle =>
        AppContext.BaseDirectory.Contains(".app/Contents/MacOS", StringComparison.OrdinalIgnoreCase);

    public static void Apply(byte[] png)
    {
        var buffer = Marshal.AllocHGlobal(png.Length);
        try
        {
            Marshal.Copy(png, 0, buffer, png.Length);
            var data = Send(objc_getClass("NSData"), sel_registerName("dataWithBytes:length:"), buffer, (nuint)png.Length);
            var image = Send(Send(objc_getClass("NSImage"), sel_registerName("alloc")), sel_registerName("initWithData:"), data);
            if (image == IntPtr.Zero)
                throw new InvalidOperationException("AppKit could not read the Dock icon.");

            var application = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            Send(application, sel_registerName("setApplicationIconImage:"), image);
            Send(image, sel_registerName("release"));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport(ObjCRuntime)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCRuntime)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr bytes, nuint length);
}
