# Third-party notices

Scrinium is licensed under the MIT license (see `LICENSE`). It uses the following components under their own licenses. All of them are NuGet packages that are restored at build time.

| Component | Used for | License |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | desktop UI | MIT |
| [Inter](https://github.com/rsms/inter), via `Avalonia.Fonts.Inter` | interface font | SIL Open Font License 1.1 |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM helpers | MIT |
| [OllamaSharp](https://github.com/awaescher/OllamaSharp) | talking to an Ollama server | MIT |
| [PdfPig](https://github.com/UglyToad/PdfPig) | reading text from PDF files | Apache License 2.0 |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) and SQLite | the local database | MIT; SQLite is in the public domain |
| [Microsoft.Extensions.AI](https://github.com/dotnet/extensions), Hosting, DependencyInjection | model abstractions and composition | MIT |
| [ASP.NET Core](https://github.com/dotnet/aspnetcore) | the HTTP server | MIT |

## Models

Scrinium does not include or download any model by itself. The models you run through Ollama (for example `bge-m3` for embeddings and a chat model of your choice) come with their own licenses and terms, which you accept when you pull them.
