namespace Blocke.X12.Models;

public sealed record LoopCtx(
    string LoopId,
    int Iteration,
    string? Selector = null)
{
    public string Render() =>
        Selector is null
            ? $"{LoopId}[#{Iteration}]"
            : $"{LoopId}[{Selector}#{Iteration}]";

    public static string Render(IReadOnlyList<LoopCtx> path) =>
        path.Count == 0 ? string.Empty : "/" + string.Join(" -> ", path.Select(p => p.Render()));
}
