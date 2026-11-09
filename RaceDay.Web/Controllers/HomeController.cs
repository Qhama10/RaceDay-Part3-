using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    public HomeController(IRaceDayApi api) : base(api) { }

    /// <summary>Participant home: upcoming events with filters. Organisers are sent to their dashboard.</summary>
    [SessionAuthorize]
    public async Task<IActionResult> Index(string? type, string? location, DateOnly? date)
    {
        if (Me!.IsOrganiser) return RedirectToAction("Dashboard", "Organiser");

        var model = new HomeViewModel { Type = type, Location = location, Date = date };
        var result = await Api.GetUpcomingEventsAsync(type, location, date);
        if (result.Unauthorized) return SessionExpired();
        if (result.Success) model.Events = result.Data ?? new();
        else FlashError(result.Error ?? "We could not load the events.");
        return View(model);
    }

    /// <summary>Full event information and its categories (any logged-in user).</summary>
    [SessionAuthorize]
    public async Task<IActionResult> Details(int id)
    {
        var result = await Api.GetEventAsync(id);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We could not find that event.");
            return HomeFor(Me!);
        }

        var model = new EventDetailsViewModel
        {
            Event = result.Data,
            IsOwner = Me!.IsOrganiser && result.Data.OrganiserId == Me.Id
        };

        if (Me.IsParticipant)
        {
            var mine = await Api.GetMyEnrolmentsAsync();
            if (mine.Unauthorized) return SessionExpired();
            model.MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id && e.Status != "Withdrawn");
        }
        return View(model);
    }

    /// <summary>Friendly error page. Never shows exception details.</summary>
    [ResponseCache(NoStore = true), IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        ViewBag.Code = code ?? 500;
        ViewBag.Title = code == 404 ? "Page not found" : "Something went wrong";
        ViewBag.Message = code == 404
            ? "We could not find the page you were looking for."
            : "Something went wrong on our side. Please try again in a moment.";
        return View();
    }
}
