using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Dtos;
using Raceday_API__prog_part_2_.Models;

namespace Raceday_API__prog_part_2_.Controllers;

public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    private static CategoryResponse ToDto(Category c) =>
        new(c.CategoryId, c.EventId, c.CategoryName, c.CategoryDescription, c.CategoryDistance);

    /// <summary>List the categories of an event.</summary>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId)) return NotFound();
        var list = await _db.Categories.AsNoTracking().Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryName).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    /// <summary>Create a category for an event you own (Organisers only).</summary>
    /// <response code="201">The created category.</response>
    /// <response code="403">Not an organiser, or you do not own the event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create(int eventId, CategoryRequest req)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid();

        var c = new Category
        {
            EventId = eventId,
            CategoryName = req.CategoryName.Trim(),
            CategoryDescription = req.CategoryDescription.Trim(),
            CategoryDistance = req.CategoryDistance.Trim()
        };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync();
        return Created($"/api/events/{eventId}/categories", ToDto(c));
    }

    /// <summary>Update a category (organiser of the parent event only).</summary>
    /// <response code="200">The updated category.</response>
    /// <response code="403">Not the organiser of the parent event.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpPut("api/categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, CategoryRequest req)
    {
        var c = await _db.Categories.Include(x => x.Event).FirstOrDefaultAsync(x => x.CategoryId == id);
        if (c == null) return NotFound();
        if (c.Event!.OrganiserId != CurrentUserId) return Forbid();

        c.CategoryName = req.CategoryName.Trim();
        c.CategoryDescription = req.CategoryDescription.Trim();
        c.CategoryDistance = req.CategoryDistance.Trim();
        await _db.SaveChangesAsync();
        return Ok(ToDto(c));
    }

    /// <summary>Delete a category and its enrolments/results (organiser of the parent event only).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Not the organiser of the parent event.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpDelete("api/categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(204)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Categories.Include(x => x.Event).FirstOrDefaultAsync(x => x.CategoryId == id);
        if (c == null) return NotFound();
        if (c.Event!.OrganiserId != CurrentUserId) return Forbid();

        var enrolments = await _db.Enrolments.Include(e => e.Result).Where(e => e.CategoryId == id).ToListAsync();
        _db.Results.RemoveRange(enrolments.Where(e => e.Result != null).Select(e => e.Result!));
        _db.Enrolments.RemoveRange(enrolments);
        _db.Categories.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}