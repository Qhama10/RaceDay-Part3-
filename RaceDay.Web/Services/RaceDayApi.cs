using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Outcome of one API call. Controllers check Success and show Error, never raw exceptions.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public HttpStatusCode StatusCode { get; init; }
    public string? Error { get; init; }
    public bool Unauthorized => StatusCode == HttpStatusCode.Unauthorized;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}

public interface IRaceDayApi
{
    // Auth
    Task<ApiResult<LoginResponseDto>> LoginAsync(LoginRequestDto req);
    Task<ApiResult> RegisterAsync(RegisterRequestDto req);

    // Events
    Task<ApiResult<List<EventDto>>> GetUpcomingEventsAsync(string? type, string? location, DateOnly? date);
    Task<ApiResult<EventDetailDto>> GetEventAsync(int id);
    Task<ApiResult<List<EventSummaryDto>>> GetMyEventsAsync();
    Task<ApiResult<EventDto>> CreateEventAsync(EventRequestDto req);
    Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventRequestDto req);
    Task<ApiResult> DeleteEventAsync(int id);
    Task<ApiResult<ImageUploadDto>> UploadBannerAsync(int eventId, IFormFile file);

    // Categories
    Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryRequestDto req);
    Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryRequestDto req);
    Task<ApiResult> DeleteCategoryAsync(int categoryId);

    // Enrolments and results
    Task<ApiResult<EnrolmentDto>> EnrolAsync(int eventId, int categoryId);
    Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync();
    Task<ApiResult<List<ResultDto>>> GetMyResultsAsync();
    Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId);
    Task<ApiResult<byte[]>> ExportEnrolmentsAsync(int eventId);
    Task<ApiResult<ResultDto>> SaveResultAsync(int enrolmentId, ResultRequestDto req);

    // Profile
    Task<ApiResult<ProfileDto>> GetProfileAsync();
    Task<ApiResult<ProfileDto>> UpdateProfileAsync(UpdateProfileRequestDto req);
    Task<ApiResult<ImageUploadDto>> UploadPictureAsync(IFormFile file);
}

public class RaceDayApi : IRaceDayApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ILogger<RaceDayApi> _log;

    public RaceDayApi(HttpClient http, ILogger<RaceDayApi> log) { _http = http; _log = log; }

    // ---------- Auth ----------
    public Task<ApiResult<LoginResponseDto>> LoginAsync(LoginRequestDto req) =>
        Send<LoginResponseDto>(HttpMethod.Post, "api/auth/login", JsonContent.Create(req, options: Json));

    public Task<ApiResult> RegisterAsync(RegisterRequestDto req) =>
        SendNoBody(HttpMethod.Post, "api/auth/register", JsonContent.Create(req, options: Json));

    // ---------- Events ----------
    public Task<ApiResult<List<EventDto>>> GetUpcomingEventsAsync(string? type, string? location, DateOnly? date)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(type)) query.Add("type=" + Uri.EscapeDataString(type));
        if (!string.IsNullOrWhiteSpace(location)) query.Add("location=" + Uri.EscapeDataString(location.Trim()));
        if (date.HasValue) query.Add("date=" + date.Value.ToString("yyyy-MM-dd"));
        var url = "api/events/upcoming" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return Send<List<EventDto>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventDetailDto>> GetEventAsync(int id) =>
        Send<EventDetailDto>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<List<EventSummaryDto>>> GetMyEventsAsync() =>
        Send<List<EventSummaryDto>>(HttpMethod.Get, "api/events/mine");

    public Task<ApiResult<EventDto>> CreateEventAsync(EventRequestDto req) =>
        Send<EventDto>(HttpMethod.Post, "api/events", JsonContent.Create(req, options: Json));

    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventRequestDto req) =>
        Send<EventDto>(HttpMethod.Put, $"api/events/{id}", JsonContent.Create(req, options: Json));

    public Task<ApiResult> DeleteEventAsync(int id) =>
        SendNoBody(HttpMethod.Delete, $"api/events/{id}");

    public Task<ApiResult<ImageUploadDto>> UploadBannerAsync(int eventId, IFormFile file) =>
        Send<ImageUploadDto>(HttpMethod.Post, $"api/events/{eventId}/banner", FileContent(file));

    // ---------- Categories ----------
    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryRequestDto req) =>
        Send<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", JsonContent.Create(req, options: Json));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryRequestDto req) =>
        Send<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", JsonContent.Create(req, options: Json));

    public Task<ApiResult> DeleteCategoryAsync(int categoryId) =>
        SendNoBody(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ---------- Enrolments and results ----------
    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int eventId, int categoryId) =>
        Send<EnrolmentDto>(HttpMethod.Post, $"api/events/{eventId}/enrol",
            JsonContent.Create(new EnrolRequestDto { CategoryId = categoryId }, options: Json));

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        Send<List<EnrolmentDto>>(HttpMethod.Get, "api/users/me/enrolments");

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        Send<List<ResultDto>>(HttpMethod.Get, "api/users/me/results");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        Send<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public async Task<ApiResult<byte[]>> ExportEnrolmentsAsync(int eventId)
    {
        try
        {
            using var resp = await _http.GetAsync($"api/events/{eventId}/enrolments/export");
            if (!resp.IsSuccessStatusCode)
                return Fail<byte[]>(resp.StatusCode, await ReadError(resp));
            return new ApiResult<byte[]>
            {
                Success = true,
                StatusCode = resp.StatusCode,
                Data = await resp.Content.ReadAsByteArrayAsync()
            };
        }
        catch (Exception ex) when (IsTransport(ex))
        {
            return Unavailable<byte[]>(ex);
        }
    }

    public Task<ApiResult<ResultDto>> SaveResultAsync(int enrolmentId, ResultRequestDto req) =>
        Send<ResultDto>(HttpMethod.Put, $"api/enrolments/{enrolmentId}/result", JsonContent.Create(req, options: Json));

    // ---------- Profile ----------
    public Task<ApiResult<ProfileDto>> GetProfileAsync() =>
        Send<ProfileDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<ProfileDto>> UpdateProfileAsync(UpdateProfileRequestDto req) =>
        Send<ProfileDto>(HttpMethod.Put, "api/users/me", JsonContent.Create(req, options: Json));

    public Task<ApiResult<ImageUploadDto>> UploadPictureAsync(IFormFile file) =>
        Send<ImageUploadDto>(HttpMethod.Post, "api/users/me/picture", FileContent(file));

    // ---------- Plumbing ----------
    private static MultipartFormDataContent FileContent(IFormFile file)
    {
        var form = new MultipartFormDataContent();
        var part = new StreamContent(file.OpenReadStream());
        if (!string.IsNullOrWhiteSpace(file.ContentType))
            part.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        form.Add(part, "file", file.FileName);
        return form;
    }

    private async Task<ApiResult<T>> Send<T>(HttpMethod method, string url, HttpContent? content = null)
    {
        try
        {
            using var req = new HttpRequestMessage(method, url) { Content = content };
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return Fail<T>(resp.StatusCode, await ReadError(resp));

            var data = await resp.Content.ReadFromJsonAsync<T>(Json);
            return new ApiResult<T> { Success = true, StatusCode = resp.StatusCode, Data = data };
        }
        catch (Exception ex) when (IsTransport(ex))
        {
            return Unavailable<T>(ex);
        }
    }

    private async Task<ApiResult> SendNoBody(HttpMethod method, string url, HttpContent? content = null)
    {
        try
        {
            using var req = new HttpRequestMessage(method, url) { Content = content };
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var error = await ReadError(resp);
                return new ApiResult { Success = false, StatusCode = resp.StatusCode, Error = error };
            }
            return new ApiResult { Success = true, StatusCode = resp.StatusCode };
        }
        catch (Exception ex) when (IsTransport(ex))
        {
            _log.LogWarning(ex, "API call failed: {Url}", url);
            return new ApiResult
            {
                Success = false,
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Error = "The RaceDay service is currently unavailable. Please try again shortly."
            };
        }
    }

    private static bool IsTransport(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException;

    private ApiResult<T> Unavailable<T>(Exception ex)
    {
        _log.LogWarning(ex, "API call failed");
        return Fail<T>(HttpStatusCode.ServiceUnavailable,
            "The RaceDay service is currently unavailable. Please try again shortly.");
    }

    private static ApiResult<T> Fail<T>(HttpStatusCode code, string error) =>
        new() { Success = false, StatusCode = code, Error = error };

    /// <summary>Turns an API error response into one friendly sentence. Raw JSON is never shown to users.</summary>
    private static async Task<string> ReadError(HttpResponseMessage resp)
    {
        string? detail = null;
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = errors.EnumerateObject()
                            .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array
                                ? p.Value.EnumerateArray().Select(v => v.GetString())
                                : Enumerable.Empty<string?>())
                            .Where(m => !string.IsNullOrWhiteSpace(m));
                        detail = string.Join(" ", messages);
                    }
                    if (string.IsNullOrWhiteSpace(detail) && root.TryGetProperty("detail", out var d)
                        && d.ValueKind == JsonValueKind.String)
                        detail = d.GetString();
                }
            }
        }
        catch (JsonException) { /* not JSON: fall through to the generic message */ }

        if (!string.IsNullOrWhiteSpace(detail) && resp.StatusCode != HttpStatusCode.InternalServerError)
            return detail!;

        return resp.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Please check the details you entered and try again.",
            HttpStatusCode.Unauthorized => "Your session has expired. Please log in again.",
            HttpStatusCode.Forbidden => "You do not have permission to do that.",
            HttpStatusCode.NotFound => "We could not find what you were looking for.",
            HttpStatusCode.Conflict => "That conflicts with existing information.",
            _ => "Something went wrong on our side. Please try again."
        };
    }
}

/// <summary>Adds the logged-in user's JWT (kept in session) to every API request.</summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _ctx;
    public BearerTokenHandler(IHttpContextAccessor ctx) => _ctx = ctx;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = _ctx.HttpContext?.Session.CurrentUser()?.Token;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
