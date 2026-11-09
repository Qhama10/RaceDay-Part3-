using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = Roles.Organiser)]
public class OrganiserController : AppController
{
    private static readonly Regex FinishTimePattern = new(@"^\d{1,2}:[0-5]\d:[0-5]\d$", RegexOptions.Compiled);

    public OrganiserController(IRaceDayApi api) : base(api) { }

    // ---------- Dashboard and events list ----------
    public async Task<IActionResult> Dashboard()
    {
        var result = await Api.GetMyEventsAsync();
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) FlashError(result.Error ?? "We could not load your events.");
        return View(result.Data ?? new());
    }

    public async Task<IActionResult> Events()
    {
        var result = await Api.GetMyEventsAsync();
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) FlashError(result.Error ?? "We could not load your events.");
        return View(result.Data ?? new());
    }

    // ---------- Create / edit / delete event ----------
    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new EventFormViewModel { EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)) });

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await Api.CreateEventAsync(ToRequest(model));
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError("", result.Error ?? "We could not create the event.");
            return View("EventForm", model);
        }

        // The event is saved. A banner problem should not lose it, so report it separately.
        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadBannerAsync(result.Data.EventId, model.Banner);
            if (upload.Unauthorized) return SessionExpired();
            if (!upload.Success)
            {
                FlashError($"The event was created, but the banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(EditEvent), new { id = result.Data.EventId });
            }
        }

        FlashSuccess("Event created. Now add some categories for participants to choose from.");
        return RedirectToAction(nameof(Categories), new { eventId = result.Data.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var result = await Api.GetEventAsync(id);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We could not find that event.");
            return RedirectToAction(nameof(Events));
        }
        if (result.Data.OrganiserId != Me!.Id) return RedirectToAction("AccessDenied", "Account");

        var e = result.Data;
        return View("EventForm", new EventFormViewModel
        {
            EventId = e.EventId,
            EventName = e.EventName,
            EventDescription = e.EventDescription,
            EventDate = e.EventDate,
            EventLocation = e.EventLocation,
            Distance = e.Distance,
            EventType = e.EventType,
            BannerUrl = e.BannerUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await Api.UpdateEventAsync(id, ToRequest(model));
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "We could not save the event.");
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadBannerAsync(id, model.Banner);
            if (upload.Unauthorized) return SessionExpired();
            if (!upload.Success)
            {
                FlashError($"Your changes were saved, but the banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(EditEvent), new { id });
            }
        }

        FlashSuccess("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await Api.DeleteEventAsync(id);
        if (result.Unauthorized) return SessionExpired();
        if (result.Success) FlashSuccess("Event deleted.");
        else FlashError(result.Error ?? "We could not delete that event.");
        return RedirectToAction(nameof(Events));
    }

    // ---------- Categories ----------
    [HttpGet]
    public async Task<IActionResult> Categories(int eventId)
    {
        var ev = await LoadOwnedEvent(eventId);
        if (ev.Redirect != null) return ev.Redirect;
        return View(new CategoriesPageViewModel
        {
            Event = ev.Event!,
            NewCategory = new CategoryFormViewModel { EventId = eventId }
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory([Bind(Prefix = "NewCategory")] CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var ev = await LoadOwnedEvent(model.EventId);
            if (ev.Redirect != null) return ev.Redirect;
            return View("Categories", new CategoriesPageViewModel { Event = ev.Event!, NewCategory = model });
        }

        var result = await Api.CreateCategoryAsync(model.EventId, ToRequest(model));
        if (result.Unauthorized) return SessionExpired();
        if (result.Success) FlashSuccess("Category added.");
        else FlashError(result.Error ?? "We could not add that category.");
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> EditCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid || model.CategoryId is null)
        {
            FlashError("Please fill in every category field.");
            return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
        }

        var result = await Api.UpdateCategoryAsync(model.CategoryId.Value, ToRequest(model));
        if (result.Unauthorized) return SessionExpired();
        if (result.Success) FlashSuccess("Category updated.");
        else FlashError(result.Error ?? "We could not update that category.");
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int categoryId, int eventId)
    {
        var result = await Api.DeleteCategoryAsync(categoryId);
        if (result.Unauthorized) return SessionExpired();
        if (result.Success) FlashSuccess("Category deleted.");
        else FlashError(result.Error ?? "We could not delete that category.");
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // ---------- Enrolments ----------
    [HttpGet]
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var result = await Api.GetEventEnrolmentsAsync(eventId);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            FlashError(result.Error ?? "We could not load the enrolments.");
            return RedirectToAction(nameof(Events));
        }

        var ev = await Api.GetEventAsync(eventId);
        return View(new EnrolmentsPageViewModel
        {
            EventId = eventId,
            EventName = ev.Data?.EventName ?? "Event",
            Enrolments = result.Data ?? new()
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportEnrolments(int eventId)
    {
        var result = await Api.ExportEnrolmentsAsync(eventId);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We could not export the enrolments.");
            return RedirectToAction(nameof(Enrolments), new { eventId });
        }
        return File(result.Data, "text/csv", $"event-{eventId}-enrolments.csv");
    }

    // ---------- Results ----------
    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var result = await Api.GetEventEnrolmentsAsync(eventId);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            FlashError(result.Error ?? "We could not load the participants.");
            return RedirectToAction(nameof(Events));
        }

        var ev = await Api.GetEventAsync(eventId);
        return View(new EnrolmentsPageViewModel
        {
            EventId = eventId,
            EventName = ev.Data?.EventName ?? "Event",
            // Withdrawn participants cannot have a result.
            Enrolments = (result.Data ?? new()).Where(e => e.Status != "Withdrawn").ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveResult(ResultInputModel input)
    {
        TimeSpan? finishTime = null;
        if (!string.IsNullOrWhiteSpace(input.FinishTime))
        {
            var text = input.FinishTime.Trim();
            if (!FinishTimePattern.IsMatch(text) || !TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                FlashError("Enter the finish time as hh:mm:ss, for example 01:23:45.");
                return RedirectToAction(nameof(Results), new { eventId = input.EventId });
            }
            finishTime = parsed;
        }
        if (input.Position is < 1)
        {
            FlashError("The finishing position must be 1 or higher.");
            return RedirectToAction(nameof(Results), new { eventId = input.EventId });
        }
        if (finishTime is null && input.Position is null)
        {
            FlashError("Enter a finish time or a finishing position.");
            return RedirectToAction(nameof(Results), new { eventId = input.EventId });
        }

        var result = await Api.SaveResultAsync(input.EnrolmentId, new ResultRequestDto
        {
            FinishTime = finishTime,
            Position = input.Position,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        });
        if (result.Unauthorized) return SessionExpired();
        if (result.Success) FlashSuccess("Result saved and published.");
        else FlashError(result.Error ?? "We could not save that result.");
        return RedirectToAction(nameof(Results), new { eventId = input.EventId });
    }

    // ---------- Helpers ----------
    private static EventRequestDto ToRequest(EventFormViewModel m) => new()
    {
        EventName = m.EventName.Trim(),
        EventDescription = string.IsNullOrWhiteSpace(m.EventDescription) ? null : m.EventDescription.Trim(),
        EventDate = m.EventDate,
        EventLocation = m.EventLocation.Trim(),
        Distance = m.Distance,
        EventType = m.EventType
    };

    private static CategoryRequestDto ToRequest(CategoryFormViewModel m) => new()
    {
        CategoryName = m.CategoryName.Trim(),
        CategoryDescription = m.CategoryDescription.Trim(),
        CategoryDistance = m.CategoryDistance.Trim()
    };

    /// <summary>Loads an event and makes sure it belongs to the logged-in organiser.</summary>
    private async Task<(EventDetailDto? Event, IActionResult? Redirect)> LoadOwnedEvent(int eventId)
    {
        var result = await Api.GetEventAsync(eventId);
        if (result.Unauthorized) return (null, SessionExpired());
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We could not find that event.");
            return (null, RedirectToAction(nameof(Events)));
        }
        if (result.Data.OrganiserId != Me!.Id) return (null, RedirectToAction("AccessDenied", "Account"));
        return (result.Data, null);
    }
}
