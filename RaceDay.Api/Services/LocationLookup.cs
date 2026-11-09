namespace Raceday_API__prog_part_2_.Services;

public record Coordinates(string Name, double Latitude, double Longitude);

public static class LocationLookup
{
    private static readonly Coordinates[] Known =
    {
        new("Durban", -29.8587, 31.0218),
        new("Cape Town", -33.9249, 18.4241),
        new("Soweto", -26.2485, 27.8540),
        new("Johannesburg", -26.2041, 28.0473),
        new("Pretoria", -25.7479, 28.2293),
        new("Gqeberha", -33.9608, 25.6022)
    };

    // Finds a known place whose name appears in the event's location text.
    public static Coordinates? Find(string? eventLocation)
    {
        if (string.IsNullOrWhiteSpace(eventLocation)) return null;
        return Known.FirstOrDefault(k =>
            eventLocation.Contains(k.Name, StringComparison.OrdinalIgnoreCase));
    }
}