using Elsa.Studio;
using Elsa.Studio.Contracts;
using Elsa.Studio.Models;
using Microsoft.AspNetCore.Components;
using RIoT2.Elsa.Studio.Components;
using System.Reflection;


namespace RIoT2.Elsa.Studio.UIProviders
{
    public class RIoTOutputSelectorUIHintHandler : IUIHintHandler
    {
        //public string UIHint => "riot-output-selector";
        public bool GetSupportsUIHint(string uiHint) => uiHint == "riot-output-selector";
        public string UISyntax => WellKnownSyntaxNames.Object;

        public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
        {
            return builder =>
            {
                builder.OpenComponent(0, typeof(RIoT2.Elsa.Studio.Components.RIoTOutputSelectorUIHint));
                builder.AddAttribute(1, nameof(RIoTOutputSelectorUIHint.EditorContext), context);
                builder.CloseComponent();
            };
        }
    }
}