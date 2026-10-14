# Scrinium

Local RAG app: Avalonia desktop + ASP.NET Core server over one core, models through Ollama. .NET 10.

## Language

- Everything the app shows or sends to a model (UI text, errors, prompts) is English.
- Chat replies to the user are Russian. Russian is fine as test data.

## Commands

```bash
dotnet build
dotnet test -c Release                      # ~1 s, includes architecture tests
dotnet run --project src/Scrinium.Desktop
dotnet run --project src/Scrinium.Server -- --urls http://localhost:5088 --Scrinium:DocumentsFolder=<dir>
dotnet run --project tools/Scrinium.Eval -c Release -- \
  --documents demo/<company>/documents --cases demo/<company>/evaluation.json \
  --chat-model qwen3-coder:30b --report demo/<company>/reports/<date>-<model>.md [--only <case-id>]
```

Warnings are errors. Package versions live in `Directory.Packages.props`.

## Architecture

- `Scrinium.Core` depends on nothing in the solution. `Storage`, `Extraction` and `Ollama` depend only on Core. `Desktop` and `Server` compose them.
- Each outside library appears in exactly one project. `ArchitectureTests` fail when a dependency points the wrong way.
- Core has no boolean properties and no mutable public properties: model a state as an enum or a type.
- Models are reached only through `IChatClient` and `IEmbeddingGenerator`.

## Working rules

- Test first. Desktop logic goes into view models that tests can drive; views stay thin.
- No coroutines, no comments that restate the code, no mutation of existing objects.
- Evaluation numbers in the README come from the committed reports, via `/run-eval`. Do not type them by hand.

## Git

- Conventional commits, no attribution lines. New branch and PR per change, merged with rebase.
- `ROADMAP.local.md` stays local and is never committed.
- Screenshots in `docs/screenshots` come from the real models on the demo documents.
