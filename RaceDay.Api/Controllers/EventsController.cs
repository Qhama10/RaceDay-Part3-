using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Dtos;
using Raceday_API__prog_part_2_.Models;
using Raceday_API__prog_part_2_.Services;

namespace Raceday_API__prog_part_2_.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobService _blobs;
    public EventsController(RaceDayDbContext db, IBlobService blobs) { _db = db; _blobs = blobs; }

    private static readonly string[] Types = { "Run", "Walk", "Cycle" };
    private static EventResponse ToDto(Event e) => new(e.EventId, e.OrganiserId, e.EventName,
        e.EventDescription, e.EventDate, e.EventLocation, e.Distance, e.EventType, e.BannerUrl);

    /// <summary>List all events with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponse>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] DateOnly? date, [FromQuery] string? type, [FromQuery] string? location)
    {
        var q = _db.Events.AsNoTracking().AsQueryable();
        if (date.HasValue) q = q.Where(e => e.EventDate == date.Value);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(e => e.EventType == type);
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(e => e.EventLocation.Contains(location));
        return Ok((await q.OrderBy(e => e.EventDate).ToListAsync()).Select(ToDto));
    }

    /// <summary>List upcoming events, with the same optional filters as the main list.</summary>
    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(List<EventResponse>), 200)]
    public async Task<IActionResult> Upcoming([FromQuery] DateOnly? date, [FromQuery] string? type, [FromQuery] string? location)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var q = _db.Events.AsNoTracking().Where(e => e.EventDate >= today);
        if (date.HasValue) q = q.Where(e => e.EventDate == date.Value);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(e => e.EventType == type);
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(e => e.EventLocation.Contains(location));
        var list = await q.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    /// <summary>Events created by the logged-in organiser, with enrolment and category counts (dashboard).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EventSummaryResponse>), 200)]
    public async Task<IActionResult> Mine()
    {
        var uid = CurrentUserId;
        var list = await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == uid)
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummaryResponse(e.EventId, e.EventName, e.EventDate, e.EventLocation,
                e.Distance, e.EventType, e.BannerUrl,
                _db.Enrolments.Count(en => en.Category!.EventId == e.EventId && en.Status != "Withdrawn"),
                e.Categories.Count))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Upload or replace the banner image of an event you own (Organisers only).</summary>
    /// <remarks>Send multipart/form-data with a single file field named "file". Images are stored in Azure Blob Storage.</remarks>
    /// <response code="200">The public URL of the stored banner.</response>
    /// <response code="400">Not a valid image, or too large.</response>
    /// <response code="403">Not an organiser, or you do not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="503">Image storage unavailable.</response>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> UploadBanner(int id, IFormFile file, CancellationToken ct)
    {
        var e = await _db.Events.FirstOrDefaultAsync(x => x.EventId == id, ct);
        if (e == null) return NotFound();
        if (e.OrganiserId != CurrentUserId) return Forbid();

        try
        {
            e.BannerUrl = await _blobs.UploadImageAsync(file, "banners", ct);
        }
        catch (ImageUploadException ex)
        {
            return ex.IsConfigurationProblem
                ? StatusCode(503, new ProblemDetails { Detail = ex.Message })
                : BadRequest(new ProblemDetails { Detail = ex.Message });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new ImageUploadResponse(e.BannerUrl));
    }

    /// <summary>Get one event with its categories.</summary>
    /// <response code="200">The event with its categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var e = await _db.Events.AsNoTracking().Include(x => x.Categories).FirstOrDefaultAsync(x => x.EventId == id);
        if (e == null) return NotFound();
        return Ok(new EventDetailResponse(e.EventId, e.OrganiserId, e.EventName, e.EventDescription, e.EventDate,
            e.EventLocation, e.Distance, e.EventType,
            e.Categories.Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.CategoryName,
                c.CategoryDescription, c.CategoryDistance)).ToList(), e.BannerUrl));
    }

    /// <summary>Create an event (Organisers only).</summary>
    /// <response code="201">The created event.</response>
    /// <response code="400">Invalid data, e.g. eventType not Run, Walk or Cycle.</response>
    /// <response code="403">Participants cannot create events.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Create(EventRequest req)
    {
        var type = Types.FirstOrDefault(t => t.Equals(req.EventType, StringComparison.OrdinalIgnoreCase));
        if (type == null) { ModelState.AddModelError("EventType", "Must be Run, Walk or Cycle."); return ValidationProblem(ModelState); }

        var e = new Event
        {
            OrganiserId = CurrentUserId,
            EventName = req.EventName,
            EventDescription = req.EventDescription,
            EventDate = req.EventDate!.Value,
            EventLocation = req.EventLocation,
            Distance = req.Distance,
            EventType = type
        };
        _db.Events.Add(e);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = e.EventId }, ToDto(e));
    }

    /// <summary>Update an event you own (Organisers only).</summary>
    /// <response code="200">The updated event.</response>
    /// <response code="403">Not an organiser, or you do not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, EventRequest req)
    {
        var type = Types.FirstOrDefault(t => t.Equals(req.EventType, StringComparison.OrdinalIgnoreCase));
        if (type == null) { ModelState.AddModelError("EventType", "Must be Run, Walk or Cycle."); return ValidationProblem(ModelState); }

        var e = await _db.Events.FirstOrDefaultAsync(x => x.EventId == id);
        if (e == null) return NotFound();
        if (e.OrganiserId != CurrentUserId) return Forbid();   // not their event

        e.EventName = req.EventName; e.EventDescription = req.EventDescription; e.EventDate = req.EventDate!.Value;
        e.EventLocation = req.EventLocation; e.Distance = req.Distance; e.EventType = type;
        await _db.SaveChangesAsync();
        return Ok(ToDto(e));
    }

    /// <summary>Delete an event you own, with its categories, enrolments and results.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Not an organiser, or you do not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(204)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.Events.Include(x => x.Categories).FirstOrDefaultAsync(x => x.EventId == id);
        if (e == null) return NotFound();
        if (e.OrganiserId != CurrentUserId) return Forbid();

        var catIds = e.Categories.Select(c => c.CategoryId).ToList();
        var enrolments = await _db.Enrolments.Include(x => x.Result).Where(x => catIds.Contains(x.CategoryId)).ToListAsync();
        _db.Results.RemoveRange(enrolments.Where(x => x.Result != null).Select(x => x.Result!));
        _db.Enrolments.RemoveRange(enrolments);   // Enrolments -> Categories isn't cascaded, so remove first
        _db.Events.Remove(e);                     // categories cascade
        await _db.SaveChangesAsync();
        return NoContent();
    }
}