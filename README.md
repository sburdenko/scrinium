# Scrinium

A local "second brain": add your text, Markdown and PDF files, then ask questions in plain language.
Every answer comes with links to the exact passages it was built from.

Everything runs on your machine. Documents, the index and the models stay local; the only network
connection the app makes is to your own Ollama server.

## What it does

1. **Add documents.** `.txt`, `.md` and `.pdf` files, via the "Add" button or drag-and-drop. Originals are copied into the app's data folder.
2. **Index them.** Each file is split into chunks and every chunk is embedded by a local model. Progress and failures are visible per document, and failed ones can be retried.
3. **Ask a question.** The question is embedded the same way and compared with all chunks.
4. **Get a grounded answer.** The most relevant passages are given to a local chat model, which answers only from them and marks every claim with `[n]`.
5. **Check the source.** Clicking a `[n]` marker or a source card opens the original text, scrolled to the sentence or list item that best matches the question and highlighted (for PDFs, on the right page).

### Answers are honest about what they know

- If the model finds no supporting fragment, it says so instead of guessing.
- If nothing in the archive is even loosely related (similarity below 0.35), the app says so, names the closest document and its similarity, and does not call the chat model at all.
- If nothing is indexed yet, it says that instead.
- Under each answer, sources the model actually cited are listed first; other retrieved fragments are shown dimmed.
- Document text is handed to the model as quoted data, not as instructions.

## Requirements

- .NET 10 SDK
- [Ollama](https://ollama.com) with an embedding model and a chat model

Developed and tested on macOS (Apple Silicon). Windows and Linux are not tested.

## Run

```bash
ollama pull bge-m3            # multilingual embeddings
ollama pull qwen3-coder:30b   # answers; any instruct model works
dotnet run --project src/Scrinium.Desktop
```

If Ollama is not running, or a model is missing, a banner at the top of the window offers to start Ollama or download the missing models.

Model names and the Ollama address are configurable in the Settings panel. Changing the embedding model requires reindexing: documents indexed with another model are marked as such and are skipped by search until reindexed ("Reindex everything" in Settings).

## Data

| What | Where (macOS) |
|---|---|
| Database | `~/Library/Application Support/Scrinium/data/scrinium.db` |
| Original files | `~/Library/Application Support/Scrinium/data/originals/<year>/<month>/` |
| Settings | `~/Library/Application Support/Scrinium/settings.json` |

Set the `SCRINIUM_DATA` environment variable to use a different data folder. Copying the `data` folder is a complete backup.

## How it works

| Step | Behaviour |
|---|---|
| Text extraction | `.txt`/`.md` read as UTF-8; PDFs read page by page with PdfPig (text layer only) |
| Chunking | Recursive: headings, blank lines, then sentences; about 400 tokens per chunk with 50 tokens of overlap. Each chunk keeps its page and character offset |
| Embeddings | Batches of 32 through `IEmbeddingGenerator`; the model name is stored per document |
| Storage | One SQLite file. Vectors are stored as normalized `float32` blobs and compared by cosine similarity in C# |
| Retrieval | Top 6 chunks; a hit must score at least `0.35` and within `0.10` of the best hit. The neighbouring chunks before and after each hit are added to the context, up to about 3000 tokens |
| Answering | Last 6 chat messages plus the fragments go to `IChatClient`; the reply is streamed |

Similarity scores from `bge-m3` are not a reliable relevance verdict: on a corpus of documents about one person, relevant and irrelevant questions both land between 0.4 and 0.55. The `0.35` threshold only discards clearly unrelated queries; whether the fragments answer the question is decided by the chat model. The values live in `RetrievalOptions` and depend on the embedding model.

## Project layout

| Project | Contents |
|---|---|
| `src/Scrinium.Core` | Extraction, chunking, storage, ingestion queue, retrieval, answering. No UI; talks to models only through `Microsoft.Extensions.AI` (`IChatClient`, `IEmbeddingGenerator`) |
| `src/Scrinium.Desktop` | Avalonia 12 desktop app (MVVM): sources list, chat with citations, original preview, settings, Ollama status banner. Ollama is wired in here |
| `tests/Scrinium.Tests` | xUnit tests on Core, using fake models, so they run without Ollama |

```bash
dotnet test tests/Scrinium.Tests
```

## Current limitations

- Single user; no accounts or access control.
- Only `.txt`, `.md` and `.pdf`. Scanned PDFs fail with a clear message (no OCR).
- Search is vector-only; exact-word matches (invoice numbers, names) are not boosted.
- Chat history lives in memory and is gone after restart.
- Documents are added by hand; no folder watching.
- Vector search scans every chunk, which is fine for thousands of chunks, not millions.
- Only the Ollama provider is wired in, although Core accepts any `IChatClient` / `IEmbeddingGenerator`.
- Answer quality depends on the models you choose, and mixed-language answers can happen.
