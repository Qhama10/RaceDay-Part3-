using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Dtos;
using Raceday_API__prog_part_2_.Models;

namespace Raceday_API__prog_part_2_.Controllers;

[Route("api/enrolments/{enrolmentId:int}/result")]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Record or update a participant's result (organiser of the event only).</summary>
    /// <remarks>Creates the result if none exists, otherwise updates it. updatedBy comes from your token.</remarks>
    /// <response code="200">The saved result.</response>
    /// <response code="400">Nothing to record, or invalid values.</response>
    /// <response code="403">Not an organiser, or the enrolment belongs to another organiser's event.</response>
    /// <response code="404">Enrolment does not exist.</response>
    [HttpPut]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Upsert(int enrolmentId, ResultRequest req)
    {
        if (req.FinishTime is null && req.Position is null)
            return BadRequest(new ProblemDetails { Detail = "Provide a finishTime or a position." });
        if (req.FinishTime is { } t && (t < TimeSpan.Zero || t >= TimeSpan.FromDays(1)))
            return BadRequest(new ProblemDetails { Detail = "finishTime must be between 00:00:00 and 23:59:59." });

        var enrolment = await _db.Enrolments
            .Include(e => e.Result)
            .Include(e => e.Category!).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment == null) return NotFound();
        if (enrolment.Category!.Event!.OrganiserId != CurrentUserId) return Forbid();

        var result = enrolment.Result;
        if (result == null)
        {
            result = new Result { EnrolmentId = enrolmentId };
            _db.Results.Add(result);
        }
        result.FinishTime = req.FinishTime;
        result.Position = req.Position;
        result.Notes = req.Notes;
        result.UpdatedBy = CurrentUserId;
        result.UpdatedAt = DateTime.Now;
        if (enrolment.Status == "Registered") enrolment.Status = "Completed";

        await _db.SaveChangesAsync();
        return Ok(new ResultResponse(result.ResultId, enrolmentId, result.FinishTime, result.Position,
            result.Notes, result.UpdatedBy, result.UpdatedAt, enrolment.Category.EventId,
            enrolment.Category.Event.EventName, enrolment.Category.CategoryName));
    }
}