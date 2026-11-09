using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Dtos;
using Raceday_API__prog_part_2_.Models;
using Raceday_API__prog_part_2_.Services;

namespace Raceday_API__prog_part_2_.Controllers;

[Route("api/users/me")]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobService _blobs;
    public UsersController(RaceDayDbContext db, IBlobService blobs) { _db = db; _blobs = blobs; }

    /// <summary>Get your own profile (shape depends on your role).</summary>
    /// <response code="200">Your profile.</response>
    /// <response code="404">Profile no longer exists.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMe()
    {
        if (CurrentRole == Roles.Organiser)
        {
            var o = await _db.Organisers.AsNoTracking().FirstOrDefaultAsync(x => x.OrganiserId == CurrentUserId);
            return o == null ? NotFound() : Ok(new ProfileResponse
            {
                Id = o.OrganiserId,
                Role = Roles.Organiser,
                Email = o.Email,
                FullName = o.FullName,
                OrganisationName = o.OrganisationName,
                ContactPhone = o.ContactPhone,
                ProfilePictureUrl = o.ProfilePictureUrl
            });
        }
        var p = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.ParticipantId == CurrentUserId);
        return p == null ? NotFound() : Ok(new ProfileResponse
        {
            Id = p.ParticipantId,
            Role = Roles.Participant,
            Email = p.Email,
            FullName = p.FullName,
            DateOfBirth = p.DateOfBirth,
            Username = p.Username,
            ProfilePictureUrl = p.ProfilePictureUrl
        });
    }

    /// <summary>Update your own profile.</summary>
    /// <remarks>Organisers: fullName, organisationName, contactPhone. Participants: fullName, dateOfBirth, username.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="404">Profile no longer exists.</response>
    /// <response code="409">Username already taken.</response>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest req)
    {
        if (CurrentRole == Roles.Organiser)
        {
            var o = await _db.Organisers.FirstOrDefaultAsync(x => x.OrganiserId == CurrentUserId);
            if (o == null) return NotFound();
            o.FullName = req.FullName.Trim();
            o.OrganisationName = req.OrganisationName?.Trim();
            o.ContactPhone = req.ContactPhone?.Trim();
            await _db.SaveChangesAsync();
            return Ok(new ProfileResponse
            {
                Id = o.OrganiserId,
                Role = Roles.Organiser,
                Email = o.Email,
                FullName = o.FullName,
                OrganisationName = o.OrganisationName,
                ContactPhone = o.ContactPhone,
                ProfilePictureUrl = o.ProfilePictureUrl
            });
        }

        var p = await _db.Participants.FirstOrDefaultAsync(x => x.ParticipantId == CurrentUserId);
        if (p == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.Username) && req.Username.Trim() != p.Username)
        {
            var username = req.Username.Trim();
            if (await _db.Participants.AnyAsync(x => x.Username == username && x.ParticipantId != p.ParticipantId))
                return Conflict(new ProblemDetails { Detail = "That username is already taken." });
            p.Username = username;
        }
        p.FullName = req.FullName.Trim();
        p.DateOfBirth = req.DateOfBirth ?? p.DateOfBirth;
        await _db.SaveChangesAsync();
        return Ok(new ProfileResponse
        {
            Id = p.ParticipantId,
            Role = Roles.Participant,
            Email = p.Email,
            FullName = p.FullName,
            DateOfBirth = p.DateOfBirth,
            Username = p.Username,
            ProfilePictureUrl = p.ProfilePictureUrl
        });
    }

    /// <summary>View your own enrolments (Participants only).</summary>
    /// <response code="200">Your enrolments with event and category details.</response>
    /// <response code="403">Organisers cannot use this endpoint.</response>
    [HttpGet("enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> MyEnrolments()
    {
        var list = await _db.Enrolments.AsNoTracking()
            .Where(e => e.ParticipantId == CurrentUserId)
            .OrderByDescending(e => e.EnrolmentDate)
            .Select(e => new EnrolmentResponse(e.EnrolmentId, e.EnrolmentDate, e.Status, e.ParticipantId, null, null,
                e.Category!.EventId, e.Category.Event!.EventName, e.CategoryId, e.Category.CategoryName,
                e.Result == null ? (TimeSpan?)null : e.Result.FinishTime,
                e.Result == null ? (int?)null : e.Result.Position))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>View your own results (Participants only).</summary>
    /// <response code="200">Your results with event details.</response>
    /// <response code="403">Organisers cannot use this endpoint.</response>
    [HttpGet("results")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> MyResults()
    {
        var uid = CurrentUserId;
        var rows = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment!.ParticipantId == uid)
            .OrderByDescending(r => r.Enrolment!.Category!.Event!.EventDate)
            .Select(r => new
            {
                r.ResultId, r.EnrolmentId, r.FinishTime, r.Position, r.Notes, r.UpdatedBy, r.UpdatedAt,
                EventId = r.Enrolment!.Category!.EventId,
                EventName = r.Enrolment.Category.Event!.EventName,
                EventDate = r.Enrolment.Category.Event.EventDate,
                CategoryName = r.Enrolment.Category.CategoryName
            })
            .ToListAsync();

        // "47th out of 312 finishers": count the results recorded for each event.
        var eventIds = rows.Select(r => r.EventId).Distinct().ToList();
        var totals = await _db.Results.AsNoTracking()
            .Where(r => eventIds.Contains(r.Enrolment!.Category!.EventId))
            .GroupBy(r => r.Enrolment!.Category!.EventId)
            .Select(g => new { EventId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Total);

        var list = rows.Select(r => new ResultResponse(r.ResultId, r.EnrolmentId, r.FinishTime, r.Position, r.Notes,
            r.UpdatedBy, r.UpdatedAt, r.EventId, r.EventName, r.CategoryName,
            totals.TryGetValue(r.EventId, out var t) ? t : null, r.EventDate)).ToList();
        return Ok(list);
    }

    /// <summary>Upload or replace your profile picture.</summary>
    /// <remarks>Send multipart/form-data with a single file field named "file". Images are stored in Azure Blob Storage.</remarks>
    /// <response code="200">The public URL of the stored picture.</response>
    /// <response code="400">Not a valid image, or too large.</response>
    /// <response code="503">Image storage unavailable.</response>
    [HttpPost("picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> UploadPicture(IFormFile file, CancellationToken ct)
    {
        string url;
        try { url = await _blobs.UploadImageAsync(file, "profiles", ct); }
        catch (ImageUploadException ex)
        {
            return ex.IsConfigurationProblem
                ? StatusCode(503, new ProblemDetails { Detail = ex.Message })
                : BadRequest(new ProblemDetails { Detail = ex.Message });
        }

        if (CurrentRole == Roles.Organiser)
        {
            var o = await _db.Organisers.FirstOrDefaultAsync(x => x.OrganiserId == CurrentUserId, ct);
            if (o == null) return NotFound();
            o.ProfilePictureUrl = url;
        }
        else
        {
            var p = await _db.Participants.FirstOrDefaultAsync(x => x.ParticipantId == CurrentUserId, ct);
            if (p == null) return NotFound();
            p.ProfilePictureUrl = url;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new ImageUploadResponse(url));
    }
}
