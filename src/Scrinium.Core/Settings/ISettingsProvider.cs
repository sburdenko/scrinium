namespace Scrinium.Core.Settings;

public interface ISettingsProvider
{
    AppSettings Current { get; }
}
