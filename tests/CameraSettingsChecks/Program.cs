using System.Text.Json.Nodes;
using KtuDeYasPortal.Panel.Application.Settings;

var count = 0;
void Check(bool value, string description) { if (!value) throw new Exception(description); count++; }
void Reject(Action action, string description)
{
    try { action(); } catch (InvalidOperationException) { count++; return; }
    throw new Exception(description);
}
var settings = new CameraDeploymentSettings();
Check(JsonNode.Parse(settings.ToJson())!["cameras"]!.AsObject().Count == 0, "No hardcoded camera");
var camera = new CameraDeploymentEntry { Id = "saha-remote-1", Mode = "remote", SourceType = "mqtt", CaptureMode = "scheduled", ClipSeconds = 33, IntervalSeconds = 91, Fps = 24, Resolution = "1920x1080", Device = "USB Test Camera", Backend = "dshow", SnapshotEnabled = false };
settings.Cameras.Add(camera);
var restored = CameraDeploymentSettings.FromJson(settings.ToJson());
Check(restored.Cameras.Single().ClipSeconds == 33 && restored.Cameras.Single().IntervalSeconds == 91 && restored.Cameras.Single().Fps == 24 && !restored.Cameras.Single().SnapshotEnabled, "Editable choices roundtrip");
Check(restored.Cameras.Single().Device == camera.Device && restored.Cameras.Single().Backend == "dshow", "Remote sender settings roundtrip");
var structure = Guid.NewGuid();
var publisher = CameraDeploymentSettings.FromJson(settings.PublisherJson(camera, structure)).Cameras.Single();
Check(publisher.SourceType == "usb" && publisher.Mode == "remote" && publisher.Device == camera.Device && publisher.CaptureMode == "scheduled" && publisher.InputTopic == $"saha/media/input/{structure}/camera-{camera.Id}/chunk", "Publisher uses actual capture choices and receiver identity");
var legacy = CameraDeploymentSettings.FromJson("""{"cameras":{"legacy":{"rtspUrlEnv":"CAMERA_OLD","eventTopic":"alarm/old","customFutureField":42}}}""");
Check(legacy.Cameras[0].CaptureMode == "event" && legacy.Cameras[0].SourceType == "rtsp" && legacy.Cameras[0].SourceEnvironment == "CAMERA_OLD", "Legacy RTSP interpretation");
Check(JsonNode.Parse(legacy.ToJson())!["cameras"]!["legacy"]!["customFutureField"]!.GetValue<int>() == 42, "Extension fields preserved");
legacy.Cameras[0].SourceValue = "rtsp://user:secret@host:8554/camera";
Check(!legacy.ToJson().Contains("secret"), "Credentials excluded from persisted camera settings");
var usb = new CameraDeploymentEntry { Id = "usb-local", SourceType = "usb", Mode = "local", Device = "/dev/video2" };
settings.Cameras.Add(usb);
Check(settings.ApplyUsbDevices("services:\n  saha-media-bridge:\n    image: test\n").Contains("devices: [\"/dev/video2:/dev/video2\"]"), "Linux device mapping in primary compose");
Check(CameraDeploymentSettings.FromJson(settings.ToJson()).Cameras.Count == 2, "Local and remote coexist");
settings.Cameras.Add(new CameraDeploymentEntry { Id = camera.Id });
Reject(() => settings.ToJson(), "Duplicate camera must fail"); settings.Cameras.RemoveAt(2);
camera.InputTopic = "saha/+/chunk";
Reject(() => settings.ToJson(), "Wildcard topic must fail"); camera.InputTopic = "";
camera.ClipSeconds = 301;
Reject(() => settings.ToJson(), "Invalid clip duration must fail"); camera.ClipSeconds = 33;
camera.Id = "Invalid Name";
Reject(() => settings.ToJson(), "Invalid identity must fail"); camera.Id = "saha-remote-1";
camera.CaptureMode = "event";
Reject(() => settings.ToJson(), "Event needs topic"); camera.EventTopic = "alarm/saha/one";
Check(CameraDeploymentSettings.FromJson(settings.ToJson()).Cameras[0].EventTopic == "alarm/saha/one", "Alarm association retained");
var persistedEnvironment = new Dictionary<string, string?> { ["SAHA_ENABLED_PIPELINES"] = "kamera", [CameraDeploymentSettings.StorageKey] = settings.ToJson() };
var environmentJson = System.Text.Json.JsonSerializer.Serialize(persistedEnvironment);
var reloadedEnvironment = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(environmentJson)!;
Check(CameraDeploymentSettings.FromJson(reloadedEnvironment[CameraDeploymentSettings.StorageKey]).Cameras[0].ClipSeconds == 33, "Central environment JSON persistence roundtrip");
camera.CaptureMode = "scheduled";
Check(JsonNode.Parse(settings.ToJson())!["cameras"]![camera.Id]!["eventTopic"] is null, "Leaving event mode removes alarm trigger");
if (args.Length > 0)
{
    Directory.CreateDirectory(args[0]);
    File.WriteAllText(Path.Combine(args[0], "receiver.json"), settings.ToJson());
    File.WriteAllText(Path.Combine(args[0], "publisher.json"), settings.PublisherJson(camera, structure));
    if (args.Length > 1) File.WriteAllText(Path.Combine(args[0], "docker-compose.yml"), settings.ApplyUsbDevices(File.ReadAllText(args[1])));
}
Console.WriteLine($"{count} camera settings checks passed.");
