using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Raceday_API__prog_part_2_.Models;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

[Table("Organisers")]
public class Organiser
{
    [Key, Column("organisers_id")] public int OrganiserId { get; set; }
    [Required, MaxLength(100), Column("organisers_email")] public string Email { get; set; } = "";
    [Required, MaxLength(100), Column("password_hash")] public string PasswordHash { get; set; } = "";
    [Required, MaxLength(100), Column("full_name")] public string FullName { get; set; } = "";
    [MaxLength(100), Column("organisation_name")] public string? OrganisationName { get; set; }
    [MaxLength(100), Column("contact_phone")] public string? ContactPhone { get; set; }
    [MaxLength(500), Column("profile_picture_url")] public string? ProfilePictureUrl { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("Participants")]
public class Participant
{
    [Key, Column("participant_id")] public int ParticipantId { get; set; }
    [Required, MaxLength(255), Column("participant_email")] public string Email { get; set; } = "";
    [Required, MaxLength(255), Column("password_hash")] public string PasswordHash { get; set; } = "";
    [Required, MaxLength(100), Column("full_name")] public string FullName { get; set; } = "";
    [Column("date_of_birth")] public DateOnly? DateOfBirth { get; set; }
    [Required, MaxLength(100), Column("username")] public string Username { get; set; } = "";
    [MaxLength(500), Column("profile_picture_url")] public string? ProfilePictureUrl { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("Events")]
public class Event
{
    [Key, Column("event_id")] public int EventId { get; set; }
    [Column("organisers_id")] public int OrganiserId { get; set; }
    [Required, MaxLength(255), Column("event_name")] public string EventName { get; set; } = "";
    [Column("event_description")] public string? EventDescription { get; set; }
    [Column("event_date")] public DateOnly EventDate { get; set; }
    [Required, MaxLength(255), Column("event_location")] public string EventLocation { get; set; } = "";
    [Column("distance", TypeName = "decimal(6,2)")] public decimal Distance { get; set; }
    [Required, MaxLength(50), Column("event_type")] public string EventType { get; set; } = "Run";
    [MaxLength(500), Column("banner_url")] public string? BannerUrl { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Organiser? Organiser { get; set; }
    public List<Category> Categories { get; set; } = new();
}

[Table("Categories")]
public class Category
{
    [Key, Column("category_id")] public int CategoryId { get; set; }
    [Required, MaxLength(100), Column("category_name")] public string CategoryName { get; set; } = "";
    [Required, MaxLength(250), Column("category_description")] public string CategoryDescription { get; set; } = "";
    [Required, MaxLength(250), Column("category_distance")] public string CategoryDistance { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.Now;
    [Column("event_id")] public int EventId { get; set; }
    public Event? Event { get; set; }
}

[Table("Enrolments")]
public class Enrolment
{
    [Key, Column("enrolment_id")] public int EnrolmentId { get; set; }
    [Column("enrolment_date")] public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    [Required, MaxLength(20), Column("status")] public string Status { get; set; } = "Registered";
    [Column("participant_id")] public int ParticipantId { get; set; }
    [Column("category_id")] public int CategoryId { get; set; }
    public Participant? Participant { get; set; }
    public Category? Category { get; set; }
    public Result? Result { get; set; }
}

[Table("Results")]
public class Result
{
    [Key, Column("results_id")] public int ResultId { get; set; }
    [Column("finish_time")] public TimeSpan? FinishTime { get; set; }
    [Column("position")] public int? Position { get; set; }
    [Column("notes")] public string? Notes { get; set; }
    [Column("updated_by")] public int UpdatedBy { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.Now;
    [Column("enrolment_id")] public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }
}