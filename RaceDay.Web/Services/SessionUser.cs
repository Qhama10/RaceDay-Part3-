using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

/// <summary>What the MVC app remembers about the logged-in user between requests.</summary>
public record SessionUser(int Id, string Name, string Email, string Role, string Token, DateTime ExpiresAt)
{
    public bool IsOrganiser => Role == Roles.Organiser;
    public bool IsParticipant => Role == Roles.Participant;
}

public static class RaceDaySessionExtensions
{
    private const string Key = "raceday.user";

    public static void SignIn(this ISession session, LoginResponseDto login)
    {
        session.Clear();
        session.SetString(Key, System.Text.Json.JsonSerializer.Serialize(new SessionUser(
            login.User.Id, login.User.FullName, login.User.Email, login.User.Role, login.Token, login.ExpiresAt)));
    }

    public static SessionUser? CurrentUser(this ISession session)
    {
        var json = session.GetString(Key);
        if (string.IsNullOrEmpty(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<SessionUser>(json); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    public static void SetDisplayName(this ISession session, string fullName)
    {
        var user = session.CurrentUser();
        if (user != null) session.SetString(Key, System.Text.Json.JsonSerializer.Serialize(user with { Name = fullName }));
    }
}

/// <summary>
/// Protects a controller or action. Not logged in (or token expired) goes to the login page.
/// Logged in with the wrong role goes to an "access denied" page.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : Attribute, Microsoft.AspNetCore.Mvc.Filters.IAuthorizationFilter
{
    public string? Role { get; set; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var user = session.CurrentUser();

        if (user == null || user.ExpiresAt <= DateTime.UtcNow)
        {
            var expired = user != null;
            session.Clear();
            var returnUrl = HttpMethods.IsGet(context.HttpContext.Request.Method)
                ? context.HttpContext.Request.Path + context.HttpContext.Request.QueryString
                : null;
            context.Result = new RedirectToActionResult("Login", "Account",
                new { returnUrl, expired = expired ? "true" : null });
            return;
        }

        if (Role != null && user.Role != Role)
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
