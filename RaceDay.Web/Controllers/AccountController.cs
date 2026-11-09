using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    public AccountController(IRaceDayApi api) : base(api) { }

    // ---------- Login ----------
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? expired = null)
    {
        if (Me is { } user) return HomeFor(user);
        if (expired == "true") FlashError("Your session has expired. Please log in again.");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await Api.LoginAsync(new LoginRequestDto { Email = model.Email.Trim(), Password = model.Password });
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError("", result.Error ?? "We could not log you in. Please try again.");
            model.Password = "";
            return View(model);
        }

        HttpContext.Session.SignIn(result.Data);
        var user = Me!;

        // Only follow a return URL that stays on this site, and only if it suits the user's role.
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            && !(user.IsParticipant && model.ReturnUrl.StartsWith("/Organiser", StringComparison.OrdinalIgnoreCase))
            && !(user.IsOrganiser && model.ReturnUrl.StartsWith("/Participant", StringComparison.OrdinalIgnoreCase)))
            return LocalRedirect(model.ReturnUrl);

        FlashSuccess($"Welcome back, {user.Name}!");
        return HomeFor(user);
    }

    // ---------- Register ----------
    [HttpGet]
    public IActionResult Register()
    {
        if (Me is { } user) return HomeFor(user);
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var isOrganiser = model.Role == Roles.Organiser;
        var isParticipant = model.Role == Roles.Participant;
        if (!isOrganiser && !isParticipant)
            ModelState.AddModelError(nameof(model.Role), "Please choose Participant or Organiser.");
        if (isParticipant && string.IsNullOrWhiteSpace(model.Username))
            ModelState.AddModelError(nameof(model.Username), "Please choose a username.");

        if (!ModelState.IsValid)
        {
            model.Password = model.ConfirmPassword = "";
            return View(model);
        }

        var request = new RegisterRequestDto
        {
            Role = model.Role,
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim(),
            Password = model.Password,
            Username = isParticipant ? model.Username?.Trim() : null,
            DateOfBirth = isParticipant ? model.DateOfBirth : null,
            OrganisationName = isOrganiser ? model.OrganisationName?.Trim() : null,
            ContactPhone = isOrganiser ? model.ContactPhone?.Trim() : null
        };

        var result = await Api.RegisterAsync(request);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "We could not create your account. Please try again.");
            model.Password = model.ConfirmPassword = "";
            return View(model);
        }

        // Log the new user straight in.
        var login = await Api.LoginAsync(new LoginRequestDto { Email = request.Email, Password = request.Password });
        if (login.Success && login.Data is not null)
        {
            HttpContext.Session.SignIn(login.Data);
            FlashSuccess($"Welcome to RaceDay, {login.Data.User.FullName}!");
            return HomeFor(Me!);
        }

        FlashSuccess("Your account has been created. Please log in.");
        return RedirectToAction(nameof(Login));
    }

    // ---------- Logout ----------
    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        FlashSuccess("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ---------- Profile (both roles) ----------
    [HttpGet, SessionAuthorize]
    public async Task<IActionResult> Profile()
    {
        var result = await Api.GetProfileAsync();
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We could not load your profile.");
            return HomeFor(Me!);
        }
        return View(ToViewModel(result.Data));
    }

    [HttpPost, SessionAuthorize]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var me = Me!;
        if (me.IsParticipant && string.IsNullOrWhiteSpace(model.Username))
            ModelState.AddModelError(nameof(model.Username), "Your username cannot be empty.");

        if (!ModelState.IsValid)
            return View(await WithDisplayFields(model));

        var result = await Api.UpdateProfileAsync(new UpdateProfileRequestDto
        {
            FullName = model.FullName.Trim(),
            Username = me.IsParticipant ? model.Username?.Trim() : null,
            DateOfBirth = me.IsParticipant ? model.DateOfBirth : null,
            OrganisationName = me.IsOrganiser ? model.OrganisationName?.Trim() : null,
            ContactPhone = me.IsOrganiser ? model.ContactPhone?.Trim() : null
        });
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "We could not save your profile.");
            return View(await WithDisplayFields(model));
        }

        HttpContext.Session.SetDisplayName(result.Data!.FullName);
        FlashSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, SessionAuthorize]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            FlashError("Please choose an image to upload.");
            return RedirectToAction(nameof(Profile));
        }

        var result = await Api.UploadPictureAsync(picture);
        if (result.Unauthorized) return SessionExpired();
        if (result.Success) FlashSuccess("Your profile picture has been updated.");
        else FlashError(result.Error ?? "We could not upload that picture.");
        return RedirectToAction(nameof(Profile));
    }

    private static ProfileViewModel ToViewModel(ProfileDto p) => new()
    {
        Role = p.Role,
        Email = p.Email,
        FullName = p.FullName,
        Username = p.Username,
        DateOfBirth = p.DateOfBirth,
        OrganisationName = p.OrganisationName,
        ContactPhone = p.ContactPhone,
        ProfilePictureUrl = p.ProfilePictureUrl
    };

    // After a failed save, the read-only fields were not posted, so fetch them again.
    private async Task<ProfileViewModel> WithDisplayFields(ProfileViewModel model)
    {
        var current = await Api.GetProfileAsync();
        if (current.Success && current.Data is not null)
        {
            model.Role = current.Data.Role;
            model.Email = current.Data.Email;
            model.ProfilePictureUrl = current.Data.ProfilePictureUrl;
        }
        return model;
    }
}
