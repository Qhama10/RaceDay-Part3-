namespace RaceDay.Web.Models;

// These classes mirror the JSON that the Part 2 API sends and receives.
// They are plain data holders: no database access and no business rules live here.

// ---------- Responses ----------
public class UserSummaryDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
}

public class LoginResponseDto
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public UserSummaryDto User { get; set; } = new();
}

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = "";
    public string? EventDescription { get; set; }
    public DateOnly EventDate { get; set; }
    public string EventLocation { get; set; } = "";
    public decimal Distance { get; set; }
    public string EventType { get; set; } = "";
    public string? BannerUrl { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = "";
    public string CategoryDescription { get; set; } = "";
    public string CategoryDistance { get; set; } = "";
}

public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
}

public class EventSummaryDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string EventLocation { get; set; } = "";
    public decimal Distance { get; set; }
    public string EventType { get; set; } = "";
    public string? BannerUrl { get; set; }
    public int EnrolmentCount { get; set; }
    public int CategoryCount { get; set; }
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = "";
    public int ParticipantId { get; set; }
    public string? ParticipantName { get; set; }
    public string? ParticipantEmail { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly? EventDate { get; set; }
    public string CategoryName { get; set; } = "";
    public int? TotalFinishers { get; set; }
}

public class ProfileDto
{
    public int Id { get; set; }
    public string Role { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? OrganisationName { get; set; }
    public string? ContactPhone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Username { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

public class ImageUploadDto
{
    public string Url { get; set; } = "";
}

// ---------- Requests ----------
public class LoginRequestDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegisterRequestDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public string? OrganisationName { get; set; }
    public string? ContactPhone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Username { get; set; }
}

public class EventRequestDto
{
    public string EventName { get; set; } = "";
    public string? EventDescription { get; set; }
    public DateOnly? EventDate { get; set; }
    public string EventLocation { get; set; } = "";
    public decimal Distance { get; set; }
    public string EventType { get; set; } = "";
}

public class CategoryRequestDto
{
    public string CategoryName { get; set; } = "";
    public string CategoryDescription { get; set; } = "";
    public string CategoryDistance { get; set; } = "";
}

public class EnrolRequestDto
{
    public int CategoryId { get; set; }
}

public class ResultRequestDto
{
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProfileRequestDto
{
    public string FullName { get; set; } = "";
    public string? OrganisationName { get; set; }
    public string? ContactPhone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Username { get; set; }
}
