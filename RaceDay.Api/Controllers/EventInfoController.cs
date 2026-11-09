using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Dtos;
using Raceday_API__prog_part_2_.Services;
using static Raceday_API__prog_part_2_.Data.RaceDayDbContext;

namespace Raceday_API__prog_part_2_.Controllers;

[Route("api/events/{id:int}")]
public class EventInfoController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IWeatherService _weather;
    public EventInfoController(RaceDayDbContext db, IWeatherService weather) { _db = db; _weather = weather; }

    /// <summary>Get map details (coordinates, Google Maps link and an embeddable map) for an event's location.</summary>
    /// <response code="200">Map details for the event.</response>
    /// <response code="404">Event missing, or its location isn't one of the known places.</response>
    [HttpGet("map")]
    [ProducesResponseType(typeof(EventMapResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMap(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null) return NotFound(new ProblemDetails { Detail = $"Event {id} does not exist." });

        var place = LocationLookup.Find(ev.EventLocation);
        if (place == null)
            return NotFound(new ProblemDetails { Detail = $"No known map coordinates for '{ev.EventLocation}'." });

        var google = FormattableString.Invariant($"https://www.google.com/maps?q={place.Latitude},{place.Longitude}");
        var embed = FormattableString.Invariant(
            $"https://www.openstreetmap.org/export/embed.html?bbox={place.Longitude - 0.05},{place.Latitude - 0.05},{place.Longitude + 0.05},{place.Latitude + 0.05}&layer=mapnik&marker={place.Latitude},{place.Longitude}");

        return Ok(new EventMapResponse(ev.EventId, ev.EventName, ev.EventLocation, place.Name,
            place.Latitude, place.Longitude, google, embed));
    }

    /// <summary>Get the current weather at an event's location (from Open-Meteo).</summary>
    /// <response code="200">Current weather.</response>
    /// <response code="404">Event missing, or its location isn't one of the known places.</response>
    /// <response code="503">The weather service could not be reached.</response>
    [HttpGet("weather")]
    [ProducesResponseType(typeof(EventWeatherResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetWeather(int id, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (ev == null) return NotFound(new ProblemDetails { Detail = $"Event {id} does not exist." });

        var place = LocationLookup.Find(ev.EventLocation);
        if (place == null)
            return NotFound(new ProblemDetails { Detail = $"No known coordinates for '{ev.EventLocation}'." });

        var w = await _weather.GetCurrentAsync(place.Latitude, place.Longitude, ct);
        if (w == null)
            return StatusCode(503, new ProblemDetails { Status = 503, Detail = "Weather service is currently unavailable." });

        return Ok(new EventWeatherResponse(ev.EventId, ev.EventName, place.Name,
            w.TemperatureC, w.WindSpeedKmh, w.Summary, "Open-Meteo"));
    }
}