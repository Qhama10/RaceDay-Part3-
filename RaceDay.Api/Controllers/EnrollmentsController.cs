using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Dtos;
using Raceday_API__prog_part_2_.Models;

namespace Raceday_API__prog_part_2_.Controllers;

[Route("api/events/{eventId:int}")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enrol in an event by choosing one of its categories (Participants only).</summary>
    /// <remarks>One active enrolment per participant per event, checked here because Enrolments links to Categories, not Events.</remarks>
    /// <response code="201">The enrolment record.</response>
    /// <response code="400">Already enrolled in this event, or the category doesn't belong to it.</response>
    /// <response code="403">Organisers cannot enrol.</response>
    /// <response code="404">Event or category does not exist.</response>
    [HttpPost("enrol")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Enrol(int eventId, EnrolRequest req)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null) return NotFound(new ProblemDetails { Detail = "Event does not exist." });

        var cat = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == req.CategoryId);
        if (cat == null) return NotFound(new ProblemDetails { Detail = "Category does not exist." });
        if (cat.EventId != eventId)
            return BadRequest(new ProblemDetails { Detail = "That category does not belong to this event." });

        var already = await _db.Enrolments.AnyAsync(e => e.ParticipantId == CurrentUserId
            && e.Category!.EventId == eventId && e.Status != "Withdrawn");
        if (already) return BadRequest(new ProblemDetails { Detail = "You are already enrolled in this event." });

        // A withdrawn enrolment in the same category would clash with the unique index, so reuse it.
        var enrolment = await _db.Enrolments.FirstOrDefaultAsync(e =>
            e.ParticipantId == CurrentUserId && e.CategoryId == req.CategoryId);
        if (enrolment == null)
        {
            enrolment = new Enrolment { ParticipantId = CurrentUserId, CategoryId = req.CategoryId };
            _db.Enrolments.Add(enrolment);
        }
        enrolment.Status = "Registered";
        enrolment.EnrolmentDate = DateTime.Now;
        await _db.SaveChangesAsync();

        return Created("/api/users/me/enrolments", new EnrolmentResponse(enrolment.EnrolmentId,
            enrolment.EnrolmentDate, enrolment.Status, CurrentUserId, null, null,
            ev.EventId, ev.EventName, cat.CategoryId, cat.CategoryName));
    }

    /// <summary>View all enrolments for an event you own (Organisers only).</summary>
    /// <response code="200">Enrolments with participant details.</response>
    /// <response code="403">Not an organiser, or you do not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid();
        return Ok(await Load(eventId, ev.EventName));
    }

    /// <summary>Export an event's enrolments as a CSV file (Organisers only).</summary>
    /// <response code="200">CSV file download.</response>
    /// <response code="403">Not an organiser, or you do not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("enrolments/export")]
    [Authorize(Roles = Roles.Organiser)]
    [Produces("text/csv")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Export(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid();

        var rows = await Load(eventId, ev.EventName);
        var sb = new StringBuilder("EnrolmentId,EnrolmentDate,Status,ParticipantName,ParticipantEmail,Category\n");
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", r.EnrolmentId, r.EnrolmentDate.ToString("yyyy-MM-dd HH:mm"),
                Csv(r.Status), Csv(r.ParticipantName), Csv(r.ParticipantEmail), Csv(r.CategoryName)));
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"event-{eventId}-enrolments.csv");
    }

    private Task<List<EnrolmentResponse>> Load(int eventId, string eventName) =>
        _db.Enrolments.AsNoTracking().Where(e => e.Category!.EventId == eventId)
            .OrderBy(e => e.EnrolmentDate)
            .Select(e => new EnrolmentResponse(e.EnrolmentId, e.EnrolmentDate, e.Status, e.ParticipantId,
                e.Participant!.FullName, e.Participant.Email, eventId, eventName, e.CategoryId, e.Category!.CategoryName,
                e.Result == null ? (TimeSpan?)null : e.Result.FinishTime,
                e.Result == null ? (int?)null : e.Result.Position))
            .ToListAsync();

    // Quotes cells, and stops spreadsheet "formula injection" (cells starting with = + - @)
    private static string Csv(string? v)
    {
        v ??= "";
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
