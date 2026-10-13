# How Scrinium is put together

This page is for anyone, not only programmers. It explains what the program is made of, what each part is allowed to do, and how a question travels through it. The technical summary is in the [README](../README.md#architecture).

## The picture

```
  your files ──▶ Ingestion ──▶ the archive ──▶ Retrieval ──▶ Answering ──▶ the window
  (txt, md,       (read, cut,     (one SQLite     (find the     (ask the      (sources,
   pdf)            embed, save)    file)           passages)     model)        chat, original)
```

You add a file. Scrinium reads its text, cuts it into passages, turns every passage into a list of numbers that stands for its meaning, and stores both. When you ask a question, the question is turned into numbers the same way and the closest passages are found. A chat model then writes an answer from those passages only, and every claim in it points back to a passage you can open.

## The six parts

Each part is a separate project in `src/`. A part may use only the parts listed in its "Uses" column. A test checks this on every build, so the table cannot quietly go stale.

| Part | What it does | Uses | Outside library |
|---|---|---|---|
| **Core** | Everything that is about *meaning*: what a document, a chunk and a citation are, how text is cut, how passages are found and ranked, how the prompt is written, how an answer is assembled. It talks to models only through two small interfaces, so it does not know which model server is behind them. | nothing | model interfaces only (Microsoft.Extensions.AI) |
| **Storage** | Keeps documents, passages and their number lists in one SQLite file and searches them. The only part that knows how that file is laid out. | Core | SQLite |
| **Extraction** | Gets plain text out of files: `.txt`, `.md` and `.pdf`. The only part that understands the PDF format. | Core | PdfPig |
| **Ollama** | Connects Core to an Ollama server: the two model interfaces, a check of what is installed, and downloading models. The only part that knows about Ollama. | Core | OllamaSharp |
| **Desktop** | The window: sources list, chat, original preview, settings, the Ollama status banner. Can start a local Ollama. | Core, Storage, Extraction, Ollama | Avalonia |
| **Server** | A small web application: a chat endpoint that streams the answer, a source preview, a document list behind a key, a health check and the chat page. Limits how often and how many at once a visitor can ask. | Core, Storage, Extraction, Ollama | ASP.NET Core |

`tests/Scrinium.Tests` checks Core with fake models, and runs the Server on a test server, so the whole test run needs no model server.

## Rules the code follows

- **One outside library per part.** SQLite lives only in Storage, PdfPig only in Extraction, OllamaSharp only in Ollama, Avalonia only in Desktop and ASP.NET Core only in Server. Replacing one touches one part.
- **Outcomes are named, not flagged.** A search does not return "found: true/false". It returns one of three things: *found* (with the passages), *nothing relevant* (with the closest document and how close it was) or *archive empty*. A test checks that Core has no yes/no properties.
- **Nothing in Core changes after it is made.** Every Core value is read-only. A test checks that too.
- **Expected failures are results, not crashes.** A PDF without text, a model server that is off, a cancelled answer: each is shown to the user as a message.
- **Documents are data, not instructions.** Text from a document reaches the model wrapped in markers and with a standing order not to obey it.
- **The model has the last word on relevance.** Similarity scores only throw out clearly unrelated questions. Whether the passages actually answer the question is decided by the chat model, which is told to say so when they do not.

## Where the answers get their sources

1. The question becomes numbers; the closest passages are found.
2. Inside each passage the sentence closest to the question is picked. That sentence, not the whole passage, is what gets highlighted in the original.
3. The model gets the whole passages, numbered 1 to N, and must cite those numbers.
4. The window turns each `[n]` into a button. A click opens the original, scrolled to the highlighted sentence.

## Words you will meet

| Word | Meaning |
|---|---|
| **Chunk** | A passage of about 400 tokens cut from a document. The unit that is searched. |
| **Token** | A piece of text as a model sees it, roughly three-quarters of a word. |
| **Embedding** | The list of numbers that stands for the meaning of a text. Texts with similar meaning get similar lists. |
| **Embedding model** | The small model that makes embeddings. It does not write text. |
| **Chat model** | The larger model that writes the answer. |
| **Similarity** | How close two embeddings are, from 0 to 1. |
| **Citation** | A pointer from a claim in the answer to a passage, shown as `[n]`. |
| **Fragment** | A passage as it is handed to the chat model, with its neighbours. |
| **Ollama** | A program that runs models on your own computer. Scrinium talks to it. |
| **Reindexing** | Making embeddings again. Needed when you switch the embedding model, because lists from different models cannot be compared. |
