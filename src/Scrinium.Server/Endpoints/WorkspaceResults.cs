namespace Scrinium.Server;

internal static class WorkspaceResults
{
    public static IResult Unknown() => TypedResults.NotFound(new ErrorDto("Unknown workspace."));
}
