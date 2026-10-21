using KtuDeYasPortal.Panel.Application.Settings;

namespace KtuDeYasPortal.Panel.Components.Pages;

public partial class Structures
{
    private string BuildStandaloneCameraEnvironment(CameraDeploymentEntry camera)
    {
        var values = new Dictionary<string, string>
        {
            ["STRUCTURE_ID"] = _createdStructureId.ToString(),
            ["DEYAS_BRIDGE_STRUCTURE_ID"] = _createdStructureId.ToString(),
            ["STRUCTURE_NAME"] = _setupName,
            ["STRUCTURE_TYPE"] = _setupStructureType.ToLowerInvariant(),
            // Each camera computer needs its own MQTT client identity.
            ["STRUCTURE_SLUG"] = ToStructureSlug(_setupName) + "-" + camera.Id,
            ["DEYAS_BRIDGE_LOCATION_ID"] = GetEnv("DEYAS_BRIDGE_LOCATION_ID"),
            ["SAHA_ENABLED_PIPELINES"] = "kamera",
            ["SAHA_MEDIA_ENABLED"] = "true",
            ["EDGE_FORWARDING_ENABLED"] = "true",
            ["SAHA_LOCAL_MQTT_ENABLED"] = "true",
            ["DEYAS_BRIDGE_SENSOR_PROVISIONING_ENABLED"] = "true",
            ["SAHA_MEDIA_CONFIG_PATH"] = $"dockerless-camera/cameras/{camera.Id}/media-config.json",
            ["MQTT_HOST"] = Uri.TryCreate(_newEdgeApiUrl, UriKind.Absolute, out var edge) ? edge.Host : "",
            ["MQTT_PORT"] = (_samePcDeployment ? _samePcMqttPort : 1883).ToString(),
            ["MQTT_USERNAME"] = "",
            ["MQTT_PASSWORD"] = ""
        };
        foreach (var key in new[] { "CENTRAL_TIMESERIES_API_BASE_URL", "CENTRAL_MQTT_HOST", "CENTRAL_MQTT_PORT", "CENTRAL_MQTT_USE_TLS" })
            values[key] = GetEnv(key);
        foreach (var key in new[] { "CENTRAL_MQTT_USERNAME", "CENTRAL_MQTT_PASSWORD", "DEYAS_JWT_SIGNING_KEY" })
            values[key] = _deploymentSecrets.GetValueOrDefault(key, "");
        // Quoting is valid for systemd EnvironmentFile; reject line injection.
        static string Quote(string value)
        {
            if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
                throw new InvalidOperationException("Kamera ortam değerleri satır sonu içeremez.");
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
        return "# Docker dışı kamera; Docker .env dosyasından bağımsızdır.\n" +
            string.Join("\n", values.Select(pair => $"{pair.Key}={Quote(pair.Value)}")) + "\n";
    }

    private static string BuildStandaloneCameraService() => """
[Unit]
Description=DEYAS USB kamera bridge
Wants=network-online.target
After=network-online.target

[Service]
Type=simple
User=deyas
SupplementaryGroups=video
WorkingDirectory=/home/deyas/Desktop/deyas-camera
EnvironmentFile=/home/deyas/Desktop/deyas-camera/saha-media-bridge.env
Environment=PYTHONUNBUFFERED=1
ExecStart=/home/deyas/Desktop/deyas-camera/.venv/bin/python /home/deyas/Desktop/deyas-camera/dockerless-camera/saha_media_bridge.py
Restart=always
RestartSec=5
KillSignal=SIGTERM
TimeoutStopSec=15

[Install]
WantedBy=multi-user.target
""";

    private static string BuildStandaloneCameraGuide() => """
# Docker dışı USB kamera

ZIP'i /home/deyas/Desktop/deyas-camera içine açın. Bu paket için Linux başlangıç adımları:

```sh
cd /home/deyas/Desktop/deyas-camera
sudo apt-get install -y python3-venv ffmpeg
python3 -m venv .venv
.venv/bin/pip install -r dockerless-camera/requirements.txt
chmod 600 saha-media-bridge.env
sudo usermod -aG video deyas
sudo cp dockerless-camera/systemd/saha-media-bridge.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now saha-media-bridge
sudo systemctl restart saha-media-bridge
sudo systemctl status saha-media-bridge --no-pager
sudo journalctl -u saha-media-bridge -n 50 --no-pager
```

Kullanıcı veya klasör yolu farklıysa service dosyasındaki User, WorkingDirectory,
EnvironmentFile ve ExecStart yollarını kurulumunuza uyarlayın.

Kökteki saha-media-bridge.env ilk uzak USB kameranın hazır ayarlarıdır.
Birden fazla kamera bilgisayarında, ilgili cameras/<kamera>/saha-media-bridge.env
 dosyasını ZIP kökündeki saha-media-bridge.env yerine kullanın.
MQTT_HOST saha broker'ının Edge adresinden türetilir; kamera bilgisayarından erişilen
adres farklıysa düzeltin. Merkezi adresler ve imza anahtarı panel seçimlerinden gelir.
Boş imza anahtarını merkezle aynı değerle tamamlayın. Cheese gibi USB kamerayı kullanan
başka uygulamaları kapatın. Pipeline ve ortam değerleri servis tarafından yüklenir.
Docker .env dosyasını bu program için kullanmayın.

Windows/macOS için DOCKER_DISI_KAMERA_KURULUMU.md kılavuzundaki işletim sistemi
adımlarını uygulayın; bağlantı değerlerini üretilmiş saha-media-bridge.env dosyasından alın.
""";
}
