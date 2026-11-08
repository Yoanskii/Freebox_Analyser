namespace FreeNetworkMonitor.Core;
public sealed class SessionTracker
{
    public Session? Active { get; private set; }
    DateTimeOffset? absentSince;
    bool manual;
    readonly HashSet<string> suppressed = [];
    public Session Begin(string name, string? executable, DateTimeOffset now, bool isManual = false)
    {
        if (Active is not null) throw new InvalidOperationException("Une session est déjà active.");
        manual = isManual; absentSince = null;
        return Active = new(Guid.NewGuid().ToString("N"), name, now, null, now.AddMinutes(-15), Executable: executable);
    }
    public Session? End(DateTimeOffset now, string state = "Complète", bool suppressAuto = false)
    {
        if (Active is null) return null;
        if(suppressAuto && Active.Executable is string id) suppressed.Add(id);
        var ended = Active with { End = now, State = state }; Active = null; absentSince = null; manual = false; return ended;
    }
    // Un seul jeu prioritaire, ordre du catalogue. Une session active conserve sa priorité.
    public (Session? Started, Session? Ended) Observe(IReadOnlyList<Game> games, IReadOnlySet<string> runningIds, DateTimeOffset now, TimeSpan grace)
    {
        suppressed.RemoveWhere(id => !runningIds.Contains(id));
        if (manual) return (null, null);
        if (Active is not null)
        {
            if (runningIds.Contains(Active.Executable ?? "")) { absentSince = null; return (null, null); }
            absentSince ??= now;
            if (now - absentSince < grace) return (null, null);
            return (null, End(now));
        }
        var game = games.FirstOrDefault(g => g.Enabled && runningIds.Contains(g.Id) && !suppressed.Contains(g.Id));
        return game is null ? (null, null) : (Begin(game.Name, game.Id, now), null);
    }
}
