using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email."), EmailAddress(ErrorMessage = "That does not look like a valid email.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please choose how you will use RaceDay.")]
    public string Role { get; set; } = "Participant";

    [Required(ErrorMessage = "Please enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email."), EmailAddress(ErrorMessage = "That does not look like a valid email."), StringLength(100)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please choose a password."), MinLength(8, ErrorMessage = "Your password must be at least 8 characters."), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    // Participant only
    [StringLength(100)] public string? Username { get; set; }
    [Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }

    // Organiser only
    [StringLength(100), Display(Name = "Organisation name")] public string? OrganisationName { get; set; }
    [StringLength(100), Display(Name = "Contact phone")] public string? ContactPhone { get; set; }
}

public class ProfileViewModel
{
    // Display only (not editable)
    public string Role { get; set; } = "";
    public string Email { get; set; } = "";
    public string? ProfilePictureUrl { get; set; }

    [Required(ErrorMessage = "Please enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    // Participant
    [StringLength(100)] public string? Username { get; set; }
    [Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }

    // Organiser
    [StringLength(100), Display(Name = "Organisation name")] public string? OrganisationName { get; set; }
    [StringLength(100), Display(Name = "Contact phone")] public string? ContactPhone { get; set; }
}

public class HomeViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Type { get; set; }
    public string? Location { get; set; }
    public DateOnly? Date { get; set; }
    public bool HasFilters => !string.IsNullOrWhiteSpace(Type) || !string.IsNullOrWhiteSpace(Location) || Date.HasValue;
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public bool IsOwner { get; set; }
    public EnrolmentDto? MyEnrolment { get; set; }
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(255)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = "";

    [StringLength(4000)]
    [Display(Name = "Description")]
    public string? EventDescription { get; set; }

    [Required(ErrorMessage = "Please choose the event date.")]
    [Display(Name = "Date")]
    public DateOnly? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), StringLength(255)]
    [Display(Name = "Location")]
    public string EventLocation { get; set; } = "";

    [Range(0.01, 9999.99, ErrorMessage = "Distance must be between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal Distance { get; set; }

    [Required(ErrorMessage = "Please choose the event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    public string? BannerUrl { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public bool IsEdit => EventId.HasValue;
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Please enter a category name."), StringLength(100)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = "";

    [Required(ErrorMessage = "Please enter a short description (for example the age group)."), StringLength(250)]
    [Display(Name = "Description")]
    public string CategoryDescription { get; set; } = "";

    [Required(ErrorMessage = "Please enter the distance (for example 10 km)."), StringLength(250)]
    [Display(Name = "Distance")]
    public string CategoryDistance { get; set; } = "";
}

public class CategoriesPageViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class EnrolmentsPageViewModel
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultInputModel
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
    public string? Notes { get; set; }
}
