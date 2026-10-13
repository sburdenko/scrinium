namespace Scrinium.Core.Settings;

public sealed record AppSettings(
    string OllamaUrl = "http://localhost:11434",
    string ChatModel = "qwen3-coder:30b",
    string EmbeddingModel = "bge-m3");

public interface ISettingsProvider
{
    AppSettings Current { get; }
}
