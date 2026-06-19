namespace KtuDeYasPortal.Panel.Application.Settings;

public sealed class PanelOptions
{
    public const string Section = "Panel";
    public string Title { get; set; } = "KTU DeYas Admin Panel";
    public string BaseUrl { get; set; } = "http://localhost:5060";
}
