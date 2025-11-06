using Challenge.Api.Controllers;
using System.Collections.Concurrent;

namespace Challenge.Api.Services;

public sealed class InMemoryProcessedEventStore : IProcessedEventStore
{
    private readonly ConcurrentQueue<UserEvent> _events = new();

    public void Append(UserEvent evt) => _events.Enqueue(evt);

    public IReadOnlyList<UserEvent> GetRecent(int limit)
    {
        return _events.Reverse().Take(limit).ToList();
    }
}
