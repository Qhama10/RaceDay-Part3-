using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Raceday_API__prog_part_2_.Services;

public class BlobSettings
{
    public string ConnectionString { get; set; } = "";
    public string ContainerName { get; set; } = "raceday-images";
}

/// <summary>Thrown when an upload is rejected (wrong type, too big, empty) or storage isn't configured.</summary>
public class ImageUploadException : Exception
{
    public bool IsConfigurationProblem { get; }
    public ImageUploadException(string message, bool isConfigurationProblem = false) : base(message)
        => IsConfigurationProblem = isConfigurationProblem;
}

public interface IBlobService
{
    /// <summary>Validates and uploads an image, returning its public URL.</summary>
    Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken ct = default);
}

public class BlobService : IBlobService
{
    private const long MaxBytes = 5 * 1024 * 1024;   // 5 MB
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly BlobSettings _settings;
    private readonly ILogger<BlobService> _log;
    public BlobService(BlobSettings settings, ILogger<BlobService> log) { _settings = settings; _log = log; }

    public async Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
            throw new ImageUploadException("Image storage is not configured on the server.", true);
        if (file == null || file.Length == 0)
            throw new ImageUploadException("Please choose an image file.");
        if (file.Length > MaxBytes)
            throw new ImageUploadException("The image is too large. The maximum size is 5 MB.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedTypes.TryGetValue(ext, out var contentType))
            throw new ImageUploadException("Only JPG, PNG or WEBP images are allowed.");

        // Check the file's real header, not just its name, so a renamed file is rejected.
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        stream.Position = 0;
        if (!LooksLikeImage(header, read))
            throw new ImageUploadException("That file is not a valid JPG, PNG or WEBP image.");

        try
        {
            var container = new BlobContainerClient(_settings.ConnectionString, _settings.ContainerName);
            // Blob-level public read so the browser can show the image from its URL.
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

            var blobName = $"{folder}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var blob = container.GetBlobClient(blobName);
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, ct);
            return blob.Uri.ToString();
        }
        catch (Exception ex) when (ex is Azure.RequestFailedException or FormatException or ArgumentException)
        {
            _log.LogError(ex, "Blob upload failed");
            throw new ImageUploadException("The image could not be stored right now. Please try again later.", true);
        }
    }

    private static bool LooksLikeImage(byte[] h, int len)
    {
        if (len >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return true;                          // JPEG
        if (len >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return true;           // PNG
        if (len >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                      && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return true;           // WEBP
        return false;
    }
}
