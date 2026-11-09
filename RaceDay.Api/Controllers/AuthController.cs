using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Dtos;
using Raceday_API__prog_part_2_.Models;
using Raceday_API__prog_part_2_.Services;

namespace Raceday_API__prog_part_2_.Controllers;

[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly ITokenService _tokens;
    public AuthController(RaceDayDbContext db, ITokenService tokens) { _db = db; _tokens = tokens; }

    /// <summary>Register a new Organiser or Participant based on the role field.</summary>
    /// <response code="201">Account created.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="409">Email or username already in use.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserSummary), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var isOrganiser = string.Equals(req.Role, Roles.Organiser, StringComparison.OrdinalIgnoreCase);
        var isParticipant = string.Equals(req.Role, Roles.Participant, StringComparison.OrdinalIgnoreCase);
        if (!isOrganiser && !isParticipant)
        {
            ModelState.AddModelError("Role", "Role must be 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }
        if (isParticipant && string.IsNullOrWhiteSpace(req.Username))
        {
            ModelState.AddModelError("Username", "Username is required for Participants.");
            return ValidationProblem(ModelState);
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Organisers.AnyAsync(o => o.Email == email) ||
            await _db.Participants.AnyAsync(p => p.Email == email))
            return Conflict(new ProblemDetails { Detail = "That email is already registered." });

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);   // never store the plain password

        if (isOrganiser)
        {
            var o = new Organiser
            {
                Email = email,
                PasswordHash = hash,
                FullName = req.FullName,
                OrganisationName = req.OrganisationName,
                ContactPhone = req.ContactPhone
            };
            _db.Organisers.Add(o);
            await _db.SaveChangesAsync();
            return Created("/api/users/me", new UserSummary(o.OrganiserId, o.Email, o.FullName, Roles.Organiser));
        }

        if (await _db.Participants.AnyAsync(p => p.Username == req.Username))
            return Conflict(new ProblemDetails { Detail = "That username is already taken." });

        var p = new Participant
        {
            Email = email,
            PasswordHash = hash,
            FullName = req.FullName,
            DateOfBirth = req.DateOfBirth,
            Username = req.Username!.Trim()
        };
        _db.Participants.Add(p);
        await _db.SaveChangesAsync();
        return Created("/api/users/me", new UserSummary(p.ParticipantId, p.Email, p.FullName, Roles.Participant));
    }

    /// <summary>Log in and receive a token that carries your role.</summary>
    /// <response code="200">Token and user details.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        int id = 0; string name = "", role = "", hash = "";

        var o = await _db.Organisers.FirstOrDefaultAsync(x => x.Email == email);
        if (o != null) { id = o.OrganiserId; name = o.FullName; role = Roles.Organiser; hash = o.PasswordHash; }
        else
        {
            var p = await _db.Participants.FirstOrDefaultAsync(x => x.Email == email);
            if (p != null) { id = p.ParticipantId; name = p.FullName; role = Roles.Participant; hash = p.PasswordHash; }
        }

        if (role == "" || !BCrypt.Net.BCrypt.Verify(req.Password, hash))
            return Unauthorized(new ProblemDetails { Detail = "Invalid email or password." });

        var (token, expires) = _tokens.CreateToken(id, email, name, role);
        return Ok(new LoginResponse(token, expires, new UserSummary(id, email, name, role)));
    }
}