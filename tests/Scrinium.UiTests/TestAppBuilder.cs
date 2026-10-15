using Avalonia;
using Avalonia.Headless;
using Scrinium.Desktop;
using Scrinium.UiTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Scrinium.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
