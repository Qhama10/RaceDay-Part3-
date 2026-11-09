using Microsoft.EntityFrameworkCore;
using Raceday_API__prog_part_2_.Models;

namespace Raceday_API__prog_part_2_.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Organiser>().HasIndex(o => o.Email).IsUnique();
        b.Entity<Participant>().HasIndex(p => p.Email).IsUnique();
        b.Entity<Participant>().HasIndex(p => p.Username).IsUnique();

        b.Entity<Event>()
            .ToTable(t => t.HasCheckConstraint("CK_Events_event_type", "[event_type] IN ('Run','Walk','Cycle')"));
        b.Entity<Event>().HasOne(e => e.Organiser).WithMany()
            .HasForeignKey(e => e.OrganiserId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Category>().HasOne(c => c.Event).WithMany(e => e.Categories)
            .HasForeignKey(c => c.EventId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Enrolment>()
            .ToTable(t => t.HasCheckConstraint("CK_Enrolments_status", "[status] IN ('Registered','Completed','Withdrawn')"));
        b.Entity<Enrolment>().HasIndex(e => new { e.ParticipantId, e.CategoryId }).IsUnique();
        b.Entity<Enrolment>().HasOne(e => e.Participant).WithMany()
            .HasForeignKey(e => e.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Enrolment>().HasOne(e => e.Category).WithMany()
            .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Result>().HasIndex(r => r.EnrolmentId).IsUnique();
        b.Entity<Result>().HasOne(r => r.Enrolment).WithOne(e => e.Result)
            .HasForeignKey<Result>(r => r.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Result>().HasOne<Organiser>().WithMany()
            .HasForeignKey(r => r.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
    }
    public record EventMapResponse(int EventId, string EventName, string EventLocation, string KnownLocation,
    double Latitude, double Longitude, string GoogleMapsUrl, string EmbedUrl);

    public record EventWeatherResponse(int EventId, string EventName, string KnownLocation,
        double TemperatureC, double WindSpeedKmh, string Summary, string Source);
}