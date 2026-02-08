using RIoT2.Elsa.Studio.Models;

namespace RIoT2.Elsa.Studio.UIProviders
{
    public class RIoTOutputProps
    {
        public RIoTTemplateList? SelectList { get; set; }
        public string? ProviderName { get; set; }
        public bool HideEditor { get; set; } = false;
    }
}
