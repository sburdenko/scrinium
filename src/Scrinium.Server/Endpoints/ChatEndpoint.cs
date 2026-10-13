using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Scrinium.Core.Chat;
using Scrinium.Core.Retrieval;
using Scrinium.Server.Security;

namespace Scrinium.Server.Endpoints;

internal static class ChatEndpoint
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder routes) => routes.MapPost("/api/chat", HandleAsync);

    private static async Task<IResult> HandleAsync(
        ChatRequest request,
        AnswerService answers,
        ChatGate gate,
        ServerOptions options,
        HttpContext http)
    {
        var problem = ChatRequestValidator.Validate(request, options);
        if (problem is not null)
            return TypedResults.BadRequest(new ErrorDto(problem));

        if (!await gate.TryEnterAsync(http.RequestAborted))
            return TypedResults.Json(new ErrorDto("The assistant is busy, try again in a moment."), statusCode: 503);

        var history = request.History?
            .Select(item => item.Role == "assistant" ? ChatTurn.Assistant(item.Text!) : ChatTurn.User(item.Text!))
            .ToList() ?? [];

        return TypedResults.ServerSentEvents(Stream(request.Question!.Trim(), history, answers, gate, http.RequestAborted));
    }

    private static async IAsyncEnumerable<SseItem<string>> Stream(
        string question,
        IReadOnlyList<ChatTurn> history,
        AnswerService answers,
        ChatGate gate,
        [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await using var events = answers.AskAsync(question, history, ct).GetAsyncEnumerator(ct);

            while (true)
            {
                var (current, failure) = await NextAsync(events);

                if (failure is not null)
                {
                    yield return Item("error", new ErrorDto(failure));
                    yield break;
                }

                if (current is null)
                    break;

                yield return current switch
                {
                    AnswerEvent.SourcesFound sources => Item("sources", sources.Citations.Select(ToDto).ToList()),
                    AnswerEvent.TextDelta delta => Item("delta", delta.Text),
                    _ => Item("done", new { })
                };
            }

            yield return Item("done", new { });
        }
        finally
        {
            gate.Leave();
        }
    }

    private static async Task<(AnswerEvent? Event, string? Failure)> NextAsync(IAsyncEnumerator<AnswerEvent> events)
    {
        try
        {
            return await events.MoveNextAsync() ? (events.Current, null) : (null, null);
        }
        catch (HttpRequestException)
        {
            return (null, "The model server is not reachable right now.");
        }
        catch (OperationCanceledException)
        {
            return (null, null);
        }
    }

    private static SseItem<string> Item<T>(string type, T data) =>
        new(JsonSerializer.Serialize(data, Json), type);

    private static CitationDto ToDto(Citation c) => new(
        c.Number,
        c.DocumentId,
        c.DocumentTitle,
        c.Page,
        c.Date.ToString("yyyy-MM-dd"),
        c.Preview,
        c.Location.Offset,
        c.Location.Length);
}
