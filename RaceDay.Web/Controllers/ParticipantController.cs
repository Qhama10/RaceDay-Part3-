using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = Roles.Participant)]
public class ParticipantController : AppController
{
    public ParticipantController(IRaceDayApi api) : base(api) { }

    /// <summary>Enrol in an event with the chosen category. The API records it immediately.</summary>
    [HttpPost]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        if (categoryId <= 0)
        {
            FlashError("Please choose a category before enrolling.");
            return RedirectToAction("Details", "Home", new { id = eventId });
        }

        var result = await Api.EnrolAsync(eventId, categoryId);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            FlashError(result.Error ?? "We could not enrol you in that event.");
            return RedirectToAction("Details", "Home", new { id = eventId });
        }

        FlashSuccess($"You are enrolled in {result.Data?.EventName ?? "the event"}. Good luck!");
        return RedirectToAction(nameof(MyEnrolments));
    }

    public async Task<IActionResult> MyEnrolments()
    {
        var result = await Api.GetMyEnrolmentsAsync();
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) FlashError(result.Error ?? "We could not load your enrolments.");
        return View(result.Data ?? new());
    }

    public async Task<IActionResult> MyResults()
    {
        var result = await Api.GetMyResultsAsync();
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) FlashError(result.Error ?? "We could not load your results.");
        return View(result.Data ?? new());
    }
}
