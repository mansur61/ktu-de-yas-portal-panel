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
    string Status,
    bool IsProtected);

public sealed record MediaArchivePage(IReadOnlyList<MediaAsset> Items, string? NextCursor, long? TotalCount);

public interface IMediaArchiveClient
{
    Task<MediaArchivePage> ListPageAsync(string? structureId, string? deviceId, DateTime? from, DateTime? to, string? type, string? cursor, int pageSize, CancellationToken ct = default);
    Task SetProtectionAsync(MediaAsset asset, bool isProtected, CancellationToken ct = default);
    Task<string?> GetPresignedUrlAsync(string bucket, string? key, CancellationToken ct = default);
}

public sealed class MediaArchiveHttpClient : IMediaArchiveClient
{
    private readonly HttpClient _http;
    public MediaArchiveHttpClient(KtuDeYasPortal.Panel.Infrastructure.Persistence.PanelApiClients factory) => _http = factory.CreateClient("video-api");

    public async Task<MediaArchivePage> ListPageAsync(
        string? structureId, string? deviceId, DateTime? from, DateTime? to, string? type, string? cursor, int pageSize, CancellationToken ct = default)
    {
        var query = $"structureId={Uri.EscapeDataString(structureId ?? string.Empty)}&deviceId={Uri.EscapeDataString(deviceId ?? string.Empty)}&pageSize={pageSize}";
        if (from is not null) query += $"&from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        if (to is not null) query += $"&to={Uri.EscapeDataString(to.Value.ToString("O"))}";
        if (!string.IsNullOrWhiteSpace(type)) query += $"&type={Uri.EscapeDataString(type)}";
        if (!string.IsNullOrWhiteSpace(cursor)) query += $"&cursor={Uri.EscapeDataString(cursor)}";

        var response = await _http.GetFromJsonAsync<MediaArchivePageDto>($"api/media?{query}", ct)
            ?? throw new InvalidDataException("Media archive returned an empty response.");
        return new MediaArchivePage(
            response.Items.Select(item => item.ToAsset()).ToList(),
            response.NextCursor,
            response.TotalCount);
    }

    public async Task<string?> GetPresignedUrlAsync(string bucket, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var response = await _http.GetFromJsonAsync<PresignDto>($"api/presign?bucket={Uri.EscapeDataString(bucket)}&key={Uri.EscapeDataString(key)}", ct);
        return response?.Url;
    }

    public async Task SetProtectionAsync(MediaAsset asset, bool isProtected, CancellationToken ct = default)
    {
        using var response = await _http.PutAsJsonAsync($"api/media/{asset.Kind}/{asset.Id}/protection", new { IsProtected = isProtected }, ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed class PresignDto { public string? Url { get; set; } }
    private sealed class MediaArchivePageDto
    {
        public List<MediaDto> Items { get; set; } = [];
        public string? NextCursor { get; set; }
        public long? TotalCount { get; set; }
    }
    private sealed class MediaDto
    {
        public Guid Id { get; set; } public string DeviceId { get; set; } = ""; public string LocationId { get; set; } = "";
        public DateTime CapturedAt { get; set; } public string Kind { get; set; } = "";
        public int? DurationSeconds { get; set; } public int? Width { get; set; } public int? Height { get; set; }
        public string? Format { get; set; } public long? FileSizeBytes { get; set; }
        public string Bucket { get; set; } = ""; public string? ObjectKey { get; set; }
        public string? ThumbnailBucket { get; set; } public string? ThumbnailKey { get; set; }
        public string Status { get; set; } = ""; public bool IsProtected { get; set; }
        public MediaAsset ToAsset() => new(Id, DeviceId, LocationId, CapturedAt, Kind, DurationSeconds, Format, FileSizeBytes,
            Bucket, ObjectKey, ThumbnailBucket, ThumbnailKey, Status, IsProtected);
    }
    private sealed class VideoDto
    {
        public Guid Id { get; set; } public string DeviceId { get; set; } = ""; public string LocationId { get; set; } = "";
        public DateTime RecordingStart { get; set; } public int? DurationSeconds { get; set; } public string? Format { get; set; }
        public long? FileSizeBytes { get; set; } public string MinioBucket { get; set; } = "videos"; public string? MinioObjectKey { get; set; }
        public string? ThumbnailBucket { get; set; } public string? ThumbnailKey { get; set; } public string Status { get; set; } = ""; public bool IsProtected { get; set; }
        public MediaAsset ToAsset() => new(Id, DeviceId, LocationId, RecordingStart, "video", DurationSeconds, Format, FileSizeBytes, MinioBucket, MinioObjectKey, ThumbnailBucket, ThumbnailKey, Status, IsProtected);
    }
    private sealed class ImageDto
    {
        public Guid Id { get; set; } public string DeviceId { get; set; } = ""; public string LocationId { get; set; } = "";
        public DateTime CapturedAt { get; set; } public string? Format { get; set; } public long? FileSizeBytes { get; set; }
        public string MinioBucket { get; set; } = "images"; public string? MinioObjectKey { get; set; }
        public string? ThumbnailBucket { get; set; } public string? ThumbnailKey { get; set; } public string Status { get; set; } = ""; public bool IsProtected { get; set; }
        public MediaAsset ToAsset() => new(Id, DeviceId, LocationId, CapturedAt, "image", null, Format, FileSizeBytes, MinioBucket, MinioObjectKey, ThumbnailBucket, ThumbnailKey, Status, IsProtected);
    }
}
