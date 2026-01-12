using Elsa.Studio.Workflows.UI.Contracts;
using Elsa.Studio.Workflows.UI.Models;
using MudBlazor;

namespace RIoT2.Elsa.Studio.UIProviders
{
    public class AddRIoTActivityDisplaySettingsProvider : IActivityDisplaySettingsProvider
    {
        public IDictionary<string, ActivityDisplaySettings> GetSettings() => new Dictionary<string, ActivityDisplaySettings>
        {
           
            ["RIoT2.Elsa.Server.RIoT.RIoTTrigger"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.FlashOn),
        };
    }
}
