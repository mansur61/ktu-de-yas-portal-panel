using System.Reflection;
using DeYas.DigitalTwin;
using KtuDeYasPortal.Panel.Components.Pages;
using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Infrastructure.Persistence;

const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
var page = new Structures();
var pageType = typeof(Structures);
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}

void SetField(string name, object value) =>
    pageType.GetField(name, Flags)!.SetValue(page, value);

var wizardSensorId = Guid.NewGuid();
SetField("_newStructureSensors", new List<Sensor>
{
    new()
    {
        Id = wizardSensorId,
        Name = "İvme sensörü",
        DeviceId = "device-wizard",
        Metrics = "x,y,z",
        LastUpdated = DateTime.UtcNow,
        LastValue = 2.5
    }
});
var wizardSensors = (IReadOnlyList<TwinModelSensor>)pageType.GetProperty("WizardModelSensors", Flags)!.GetValue(page)!;
Check(wizardSensors.Single().DeviceId == "device-wizard", "Wizard viewer keeps DeviceId");
Check(wizardSensors.Single().Metrics == "x,y,z" && wizardSensors.Single().LastValue == 2.5, "Wizard viewer carries available reading metadata");
Check(!wizardSensors.Single().HasModelPosition, "Unplaced zero XYZ remains an unplaced sensor");

var structureId = Guid.NewGuid();
var detailSensorId = Guid.NewGuid();
var structure = new Structure
{
    Id = structureId,
    ModelUrl = "https://example.test/structure.glb",
    Sensors =
    [
        new StructureSensor
        {
            SensorId = detailSensorId,
            Name = "Gövde sensörü",
            DeviceId = "device-detail",
            SensorType = "Accelerometer",
            Metrics = "x,y,z"
        }
    ]
};
SetField("_editingStructure", structure);
var detailSensors = (IReadOnlyList<TwinModelSensor>)pageType.GetProperty("DetailModelSensors", Flags)!.GetValue(page)!;
Check(detailSensors.Count == 1 && detailSensors.Single().Id == detailSensorId, "Editor options are scoped to the selected structure");
Check(detailSensors.Single().DeviceId == "device-detail" && detailSensors.Single().Metrics == "x,y,z", "Structure viewer keeps placement identity and metrics");

pageType.GetMethod("OnDetailModelSensorChanged", Flags)!.Invoke(page, [detailSensors.Single()]);
Check((Guid?)pageType.GetField("_detailSelectedSensorId", Flags)!.GetValue(page) == detailSensorId, "SearchableSelect callback drives viewer SelectedId");
Check((bool)pageType.GetProperty("CanManageSelectedStructure", Flags)!.GetValue(page)! == false, "Structure placement is locked without management access");
await (Task)pageType.GetMethod("SaveDetailModelPositionAsync", Flags)!.Invoke(
    page, [new ModelPositionSelection(detailSensorId, 1, 2, 3, 0, 0, 0)])!;
Check(pageType.GetField("_saveError", Flags)!.GetValue(page) is string message && message.Contains("yetkiniz"), "Unauthorized placement updates are rejected");
pageType.GetMethod("ConfirmDelete", Flags)!.Invoke(page, [structureId]);
Check(pageType.GetField("_deletingId", Flags)!.GetValue(page) is null, "Unauthorized structure deletion is rejected");

SetField("_manageAccess", new PanelAccessSnapshot(false, [structureId]));
Check((bool)pageType.GetProperty("CanManageSelectedStructure", Flags)!.GetValue(page)!, "Structure management access enables editor actions");

Console.WriteLine($"{checks} structure editor checks passed.");
