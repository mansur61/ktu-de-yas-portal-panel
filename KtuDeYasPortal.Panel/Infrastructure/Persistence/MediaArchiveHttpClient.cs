using System.Net.Http.Json;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public sealed record MediaAsset(
    Guid Id,
    string DeviceId,
    string LocationId,
    DateTime CapturedAt,
    string Kind,
    int? DurationSeconds,
    string? Format,
    long? FileSizeBytes,
    string Bucket,
    string? ObjectKey,
    string? ThumbnailBucket,
    string? ThumbnailKey,
    string Status);

public interface IMediaArchiveClient
{
    Task<IReadOnlyList<MediaAsset>> ListAsync(string? structureId, string? deviceId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<string?> GetPresignedUrlAsync(string bucket, string? key, CancellationToken ct = default);
}

public sealed class MediaArchiveHttpClient : IMediaArchiveClient
{
    private readonly HttpClient _http;
    public MediaArchiveHttpClient(IHttpClientFactory factory) => _http = factory.CreateClient("video-api");

    public async Task<IReadOnlyList<MediaAsset>> ListAsync(string? structureId, string? deviceId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = $"structureId={Uri.EscapeDataString(structureId ?? string.Empty)}&deviceId={Uri.EscapeDataString(deviceId ?? string.Empty)}&limit=100";
        if (from is not null) query += $"&from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        if (to is not null) query += $"&to={Uri.EscapeDataString(to.Value.ToString("O"))}";

        var videos = await _http.GetFromJsonAsync<List<VideoDto>>($"api/videos?{query}", ct) ?? [];
        var images = await _http.GetFromJsonAsync<List<ImageDto>>($"api/images?{query}", ct) ?? [];
        return videos.Select(item => item.ToAsset()).Concat(images.Select(item => item.ToAsset()))
            .OrderByDescending(item => item.CapturedAt).ToList();
    }

    public async Task<string?> GetPresignedUrlAsync(string bucket, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var response = await _http.GetFromJsonAsync<PresignDto>($"api/presign?bucket={Uri.EscapeDataString(bucket)}&key={Uri.EscapeDataString(key)}", ct);
        return response?.Url;
    }

    private sealed class PresignDto { public string? Url { get; set; } }
    private sealed class VideoDto
    {
        public Guid Id { get; set; } public string DeviceId { get; set; } = ""; public string LocationId { get; set; } = "";
        public DateTime RecordingStart { get; set; } public int? DurationSeconds { get; set; } public string? Format { get; set; }
        public long? FileSizeBytes { get; set; } public string MinioBucket { get; set; } = "videos"; public string? MinioObjectKey { get; set; }
        public string? ThumbnailBucket { get; set; } public string? ThumbnailKey { get; set; } public string Status { get; set; } = "";
        public MediaAsset ToAsset() => new(Id, DeviceId, LocationId, RecordingStart, "video", DurationSeconds, Format, FileSizeBytes, MinioBucket, MinioObjectKey, ThumbnailBucket, ThumbnailKey, Status);
    }
    private sealed class ImageDto
    {
        public Guid Id { get; set; } public string DeviceId { get; set; } = ""; public string LocationId { get; set; } = "";
        public DateTime CapturedAt { get; set; } public string? Format { get; set; } public long? FileSizeBytes { get; set; }
        public string MinioBucket { get; set; } = "images"; public string? MinioObjectKey { get; set; }
        public string? ThumbnailBucket { get; set; } public string? ThumbnailKey { get; set; } public string Status { get; set; } = "";
        public MediaAsset ToAsset() => new(Id, DeviceId, LocationId, CapturedAt, "image", null, Format, FileSizeBytes, MinioBucket, MinioObjectKey, ThumbnailBucket, ThumbnailKey, Status);
    }
}
