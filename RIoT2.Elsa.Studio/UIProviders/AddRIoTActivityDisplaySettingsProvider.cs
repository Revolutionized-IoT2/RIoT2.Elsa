using Elsa.Studio.Workflows.UI.Contracts;
using Elsa.Studio.Workflows.UI.Models;
using MudBlazor;

namespace RIoT2.Elsa.Studio.UIProviders
{
    public class AddRIoTActivityDisplaySettingsProvider : IActivityDisplaySettingsProvider
    {
        public IDictionary<string, ActivityDisplaySettings> GetSettings() => new Dictionary<string, ActivityDisplaySettings>
        {
           
            ["RIoT2.RIoTTrigger"] = new("#007dff", Icons.Material.Outlined.FlashOn),
            ["RIoT2.RIoTData"] = new ("#fec203", Icons.Material.Outlined.DataObject),
            ["RIoT2.RIoTOutput"] = new("#00ec39", Icons.Material.Outlined.Output),
            ["RIoT2.RIoTConfirmedCommand"] = new("#00ec39", Icons.Material.Outlined.TaskAlt),
            ["RIoT2.RIoTCompare"] = new("#fec203", Icons.Material.Outlined.CompareArrows),
            ["RIoT2.RIoTThreshold"] = new("#fec203", Icons.Material.Outlined.Thermostat),
            ["RIoT2.RIoTDebounce"] = new("#9c27b0", Icons.Material.Outlined.HourglassBottom),
            ["RIoT2.RIoTThrottle"] = new("#9c27b0", Icons.Material.Outlined.Speed),
            ["RIoT2.RIoTWaitForState"] = new("#9c27b0", Icons.Material.Outlined.PendingActions),
            ["RIoT2.RIoTSchedule"] = new("#007dff", Icons.Material.Outlined.Schedule),
            ["RIoT2.RIoTNotification"] = new("#00ec39", Icons.Material.Outlined.NotificationsActive),
            ["RIoT2.RIoTWebhook"] = new("#00ec39", Icons.Material.Outlined.Webhook),
        };
    }
}
