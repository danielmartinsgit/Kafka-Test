using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Challenge.Api.Controllers;

[ApiController]
[Route("events")]
public sealed class EventsController : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "login", "logout", "purchase" };

    private readonly IEventProducer _producer;
    private readonly IProcessedEventStore _store;
    private readonly ILogger<EventsController> _log;

    public EventsController(IEventProducer producer, IProcessedEventStore store, ILogger<EventsController> log)
    {
        _producer = producer;
        _store = store;
        _log = log;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] IngestEventRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!AllowedTypes.Contains(body.Type))
            return BadRequest(new { error = "type must be one of: login, logout, purchase" });

        var evt = new UserEvent(
            UserId: body.UserId.Trim(),
            Type: body.Type.ToLowerInvariant(),
            Timestamp: body.Timestamp ?? DateTimeOffset.UtcNow,
            Data: body.Data);

        try
        {
            await _producer.PublishAsync(evt, ct);
            _log.LogInformation("Event accepted userId={UserId} type={Type} ts={Ts}", evt.UserId, evt.Type, evt.Timestamp);
            return Accepted();
        }
        catch (OperationCanceledException)
        {
            return Problem(statusCode: 499, title: "Client closed request");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to publish event");
            return Problem(statusCode: 500, title: "Failed to enqueue event");
        }
    }

    [HttpGet]
    public IActionResult Get([FromQuery] int? limit = 100)
    {
        var take = Math.Clamp(limit ?? 100, 1, 1000);
        var items = _store.GetRecent(take);
        return Ok(items);
    }
}

public interface IEventProducer
{
    Task PublishAsync(UserEvent evt, CancellationToken ct);
}

public interface IProcessedEventStore
{
    IReadOnlyList<UserEvent> GetRecent(int limit);
    void Append(UserEvent evt);
}

public sealed record IngestEventRequest(
    [property: Required, MinLength(1)] string UserId,
    [property: Required, MinLength(3)] string Type,
    DateTimeOffset? Timestamp,
    JsonElement? Data);

public sealed record UserEvent(
    string UserId,
    string Type,
    DateTimeOffset Timestamp,
    JsonElement? Data);
