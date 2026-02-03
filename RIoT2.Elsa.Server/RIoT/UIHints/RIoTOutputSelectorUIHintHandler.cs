using Elsa.Studio.Models;
using Elsa.Workflows;
using Microsoft.AspNetCore.Components;
using RIoT2.Elsa.Studio.Components;
using System.Reflection;


namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    public class RIoTOutputSelectorUIHintHandler : IUIHintHandler
    {
        public string UIHint => "riot-output-selector";


        public ValueTask<IEnumerable<Type>> GetPropertyUIHandlersAsync(PropertyInfo propertyInfo, CancellationToken cancellationToken)
        {
            return new([typeof(RIoTOutputOptionsProvider)]);
        }

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