using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KtuDeYasPortal.Panel.Application.Settings;

public sealed class CameraDeploymentSettings
{
    public const string StorageKey = "MEDIA_CONFIG_JSON";
    public List<CameraDeploymentEntry> Cameras { get; set; } = [];

    public string ToJson(bool validate = true, Guid? structureId = null)
    {
        var cameras = new JsonObject();
        var topics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var camera in Cameras)
        {
            if (validate) camera.Validate();
            if (cameras.ContainsKey(camera.Id)) throw new InvalidOperationException("Kamera kimlikleri benzersiz olmalıdır.");
            var config = camera.Config.DeepClone().AsObject();
            config["mode"] = camera.Mode;
            config["sourceType"] = camera.SourceType;
            config["captureMode"] = camera.CaptureMode;
            config["clipSeconds"] = camera.ClipSeconds;
            config["intervalSeconds"] = camera.IntervalSeconds;
            config["fps"] = camera.Fps;
            config["resolution"] = camera.Resolution;
            config["pixelFormat"] = camera.PixelFormat;
            config["enabled"] = camera.Enabled;
            config["snapshotEnabled"] = camera.SnapshotEnabled;
            if (camera.CaptureMode != "event") config.Remove("eventTopic");
            if (camera.SourceType == "usb")
            {
                config["device"] = camera.Device; config["backend"] = camera.Backend;
                config.Remove("sourceEnv"); config.Remove("rtspUrlEnv");
            }
            if (camera.SourceType == "mqtt")
            {
                config.Remove("device"); config.Remove("backend");
                config.Remove("sourceEnv"); config.Remove("rtspUrlEnv");
            }
            if (config["sourceType"]?.GetValue<string>() == "rtsp")
            {
                config["sourceEnv"] = camera.SourceEnvironment;
                config.Remove("rtspUrlEnv");
            }
            var id = structureId is { } value && value != Guid.Empty ? value.ToString("D") : null;
            var needsInputTopic = camera.SourceType == "mqtt" || camera.SourceType == "usb" && camera.Mode == "remote";
            if (needsInputTopic)
            {
                var inputTopic = camera.InputTopic;
                if (string.IsNullOrWhiteSpace(inputTopic) && id is not null)
                    inputTopic = $"saha/media/input/{id}/camera-{camera.Id}/chunk";
                if (string.IsNullOrWhiteSpace(inputTopic)) config.Remove("inputTopic");
                else config["inputTopic"] = inputTopic;
            }
            else config.Remove("inputTopic");

            var commandTopic = camera.CommandTopic;
            if (string.IsNullOrWhiteSpace(commandTopic) && id is not null)
                commandTopic = $"saha/media/commands/{id}/camera-{camera.Id}/capture";
            if (string.IsNullOrWhiteSpace(commandTopic)) config.Remove("commandTopic");
            else config["commandTopic"] = commandTopic;

            var checkedTopics = needsInputTopic
                ? new[] { config["inputTopic"]?.GetValue<string>(), config["commandTopic"]?.GetValue<string>() }
                : new[] { config["commandTopic"]?.GetValue<string>() };
            if (camera.CaptureMode == "event") checkedTopics = [.. checkedTopics, config["eventTopic"]?.GetValue<string>()];
            foreach (var topic in checkedTopics.Where(value => !string.IsNullOrWhiteSpace(value)))
                if (validate && (!topics.Add(topic!) || topic!.IndexOfAny(['+', '#']) >= 0))
                    throw new InvalidOperationException("Kamera topic'leri benzersiz olmalı ve +/# içermemelidir.");

            cameras.Add(camera.Id, config);
        }
        return new JsonObject { ["defaultCameraMode"] = "remote", ["cameras"] = cameras }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public string PublisherJson(CameraDeploymentEntry camera, Guid structureId)
    {
        var publisher = FromJson(ToJson(structureId: structureId)).Cameras.Single(entry => entry.Id == camera.Id);
        publisher.SourceType = "usb";
        publisher.Mode = "remote";
        publisher.Device = camera.Device;
        publisher.Backend = camera.Backend;
        publisher.InputTopic = string.IsNullOrWhiteSpace(camera.InputTopic)
            ? $"saha/media/input/{structureId}/camera-{camera.Id}/chunk" : camera.InputTopic;
        var settings = new CameraDeploymentSettings { Cameras = [publisher] };
        return settings.ToJson(structureId: structureId);
    }

    public string[] LinuxUsbDevices()
    {
        var devices = Cameras.Where(camera => camera.Enabled && camera.SourceType == "usb" && camera.Backend == "v4l2")
            .Select(camera => camera.Device).Distinct().ToArray();
        if (devices.Any(device => !Regex.IsMatch(device, "^/dev/video[0-9]+$")))
            throw new InvalidOperationException("Docker USB cihaz adresi /dev/videoN olmalıdır.");
        return devices.Select(device => $"{device}:{device}").ToArray();
    }

    public string ApplyUsbDevices(string compose)
    {
        var devices = LinuxUsbDevices();
        if (devices.Length == 0) return compose;
        var normalized = compose.Replace("\r\n", "\n");
        const string service = "  saha-media-bridge:\n";
        if (!normalized.Contains(service)) throw new InvalidOperationException("Compose içinde kamera servisi bulunamadı.");
        return normalized.Replace(service, service + "    devices: " + JsonSerializer.Serialize(devices) + "\n");
    }

    public static CameraDeploymentSettings FromJson(string? json)
    {
        var result = new CameraDeploymentSettings();
        if (string.IsNullOrWhiteSpace(json)) return result;
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Kamera yapılandırması nesne olmalıdır.");
        foreach (var pair in root["cameras"]?.AsObject() ?? new JsonObject())
        {
            var config = pair.Value?.DeepClone().AsObject() ?? throw new InvalidOperationException("Kamera tanımı nesne olmalıdır.");
            config["sourceType"] ??= JsonValue.Create("rtsp");
            config["mode"] ??= root["defaultCameraMode"]?.DeepClone() ?? JsonValue.Create("remote");
            config["captureMode"] ??= JsonValue.Create(config["eventTopic"] is null ? "manual" : "event");
            result.Cameras.Add(new CameraDeploymentEntry { Id = pair.Key, Config = config });
        }
        return result;
    }
}

public sealed class CameraDeploymentEntry
{
    public string Id { get; set; } = "";
    // Preserve extension fields so adding a source does not discard its settings.
    public JsonObject Config { get; set; } = new();
    private string Text(string key, string fallback) => Config[key]?.GetValue<string>() ?? fallback;
    private int Number(string key, int fallback) => Config[key]?.GetValue<int>() ?? fallback;
    private bool Flag(string key, bool fallback) => Config[key]?.GetValue<bool>() ?? fallback;
    public string Mode { get => Text("mode", "remote"); set => Config["mode"] = value; }
    public string SourceType { get => Text("sourceType", "mqtt"); set { Config["sourceType"] = value; if (value == "mqtt") Mode = "remote"; } }
    public string CaptureMode { get => Text("captureMode", "manual"); set => Config["captureMode"] = value; }
    public string Device { get => Text("device", ""); set => Config["device"] = value; }
    public string Backend { get => Text("backend", "v4l2"); set => Config["backend"] = value; }
    public string SourceEnvironment => Text("sourceEnv", Text("rtspUrlEnv", "CAMERA_" + Id.Replace('-', '_').ToUpperInvariant() + "_SOURCE"));
    public string SourceValue { get; set; } = ""; // Export only; never save RTSP credentials in the central record.
    public string InputTopic { get => Text("inputTopic", ""); set => Config["inputTopic"] = value; }
    public string CommandTopic { get => Text("commandTopic", ""); set => Config["commandTopic"] = value; }
    public string EventTopic { get => Text("eventTopic", ""); set => Config["eventTopic"] = value; }
    public string Resolution { get => Text("resolution", "1280x720"); set => Config["resolution"] = value; }
    public string PixelFormat { get => Text("pixelFormat", "auto"); set => Config["pixelFormat"] = value; }
    public int ClipSeconds { get => Number("clipSeconds", 20); set => Config["clipSeconds"] = value; }
    public int IntervalSeconds { get => Number("intervalSeconds", 60); set => Config["intervalSeconds"] = value; }
    public int Fps { get => Number("fps", 15); set => Config["fps"] = value; }
    public bool Enabled { get => Flag("enabled", true); set => Config["enabled"] = value; }
    public bool SnapshotEnabled { get => Flag("snapshotEnabled", true); set => Config["snapshotEnabled"] = value; }
    public void Validate()
    {
        if (!Regex.IsMatch(Id, "^[a-z0-9]+(-[a-z0-9]+)*$")) throw new InvalidOperationException("Kamera kimliğinde küçük harf, rakam ve tire kullanın.");
        if (Mode is not ("local" or "remote") || SourceType is not ("usb" or "rtsp" or "mqtt") || CaptureMode is not ("manual" or "scheduled" or "continuous" or "event")) throw new InvalidOperationException("Geçersiz kamera seçimi.");
        if (SourceType == "mqtt" && Mode != "remote") throw new InvalidOperationException("MQTT kamera remote olmalıdır.");
        if (ClipSeconds is < 1 or > 300 || IntervalSeconds is < 1 or > 86400 || Fps is < 1 or > 120 || !Regex.IsMatch(Resolution, "^[1-9][0-9]{1,4}x[1-9][0-9]{1,4}$")) throw new InvalidOperationException("Klip, aralık, FPS veya çözünürlük geçersiz.");
        if (Enabled && SourceType == "usb" && string.IsNullOrWhiteSpace(Device)) throw new InvalidOperationException("USB cihaz adresini doldurun.");
        if (SourceType is "usb" or "mqtt" && Backend is not ("v4l2" or "dshow" or "avfoundation")) throw new InvalidOperationException("USB işletim sistemi seçimi geçersiz.");
        if (PixelFormat is not ("auto" or "mjpeg" or "yuyv422")) throw new InvalidOperationException("USB görüntü biçimi geçersiz.");
        if (CaptureMode == "event" && string.IsNullOrWhiteSpace(EventTopic)) throw new InvalidOperationException("Olay modunda alarm topic'i zorunludur.");
        if (!Regex.IsMatch(SourceEnvironment, "^[A-Z_][A-Z0-9_]*$") || SourceValue.Contains('\n') || SourceValue.Contains('\r')) throw new InvalidOperationException("Kamera kaynak ortam değişkeni geçersiz.");
        if (SourceType == "rtsp" && SourceValue.Length > 0 && !SourceValue.StartsWith("rtsp://") && !SourceValue.StartsWith("rtsps://")) throw new InvalidOperationException("RTSP adresi rtsp:// veya rtsps:// ile başlamalıdır.");
    }
}
