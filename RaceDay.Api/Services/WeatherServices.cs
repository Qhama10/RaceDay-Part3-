using System.Globalization;
using System.Text.Json;

namespace Raceday_API__prog_part_2_.Services;

public record WeatherInfo(double TemperatureC, double WindSpeedKmh, int WeatherCode, string Summary);

public interface IWeatherService
{
    Task<WeatherInfo?> GetCurrentAsync(double latitude, double longitude, CancellationToken ct = default);
}

// Calls Open-Meteo, a free weather service that needs no API key.
public class OpenMeteoWeatherService : IWeatherService
{
    private readonly HttpClient _http;
    public OpenMeteoWeatherService(HttpClient http) => _http = http;

    public async Task<WeatherInfo?> GetCurrentAsync(double lat, double lon, CancellationToken ct = default)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,wind_speed_10m,weather_code");
        try
        {
            using var res = await _http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var cur = doc.RootElement.GetProperty("current");
            var temp = cur.GetProperty("temperature_2m").GetDouble();
            var wind = cur.GetProperty("wind_speed_10m").GetDouble();
            var code = cur.GetProperty("weather_code").GetInt32();
            return new WeatherInfo(temp, wind, code, Describe(code));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or JsonException or KeyNotFoundException)
        {
            return null;   // service down: the controller turns this into a 503
        }
    }

    private static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 or 2 => "Mainly clear / partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        >= 51 and <= 57 => "Drizzle",
        >= 61 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow",
        >= 80 and <= 82 => "Rain showers",
        >= 95 => "Thunderstorm",
        _ => "Unknown"
    };
}