using System.Reflection;
using Elsa.Workflows;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Studio.Models;
using RIoT2.Elsa.Studio.UIProviders;

namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    /// <summary>A selector of one kind of template, without the value editor.</summary>
    public abstract class RIoTTemplateSelectorProvider(IRIoTDataService rIoTData, TemplateType type) : IPropertyUIHandler
    {
        public float Priority { get; }

        public async ValueTask<IDictionary<string, object>> GetUIPropertiesAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken = default)
        {
            List<Template> templates;
            try
            {
                templates = await (type == TemplateType.Command ? rIoTData.GetCommandTemplatesAsync() : rIoTData.GetReportTemplatesAsync()).WaitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException("Error fetching RIoT Templates: " + ex.Message, ex);
            }
            var value = new RIoTOutputProps
            {
                SelectList = new RIoTTemplateList(templates.Select(t => t.Create(type)).ToList()),
                HideEditor = true
            };
            return new Dictionary<string, object> { ["riot-output-selector"] = value };
        }
    }

    /// <summary>The command templates, for <c>RIoT Notification</c>.</summary>
    public sealed class RIoTCommandOptionsProvider(IRIoTDataService rIoTData) : RIoTTemplateSelectorProvider(rIoTData, TemplateType.Command);

    /// <summary>The report templates, for <c>RIoT Wait for state</c>.</summary>
    public sealed class RIoTReportOptionsProvider(IRIoTDataService rIoTData) : RIoTTemplateSelectorProvider(rIoTData, TemplateType.Report);
}
