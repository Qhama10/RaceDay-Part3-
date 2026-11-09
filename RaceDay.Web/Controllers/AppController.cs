using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers for all controllers.</summary>
public abstract class AppController : Controller
{
    protected readonly IRaceDayApi Api;
    protected AppController(IRaceDayApi api) => Api = api;

    /// <summary>The logged-in user kept in session (not the same thing as Controller.User).</summary>
    protected SessionUser? Me => HttpContext.Session.CurrentUser();

    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;

    /// <summary>The API said 401: the token has expired, so end the session and go back to login.</summary>
    protected IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        FlashError("Your session has expired. Please log in again.");
        return RedirectToAction("Login", "Account");
    }

    /// <summary>The place a user lands after logging in, based on their role.</summary>
    protected IActionResult HomeFor(SessionUser user) =>
        user.IsOrganiser
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
}
