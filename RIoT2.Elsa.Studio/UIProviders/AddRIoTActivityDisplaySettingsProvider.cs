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
        };
    }
}
