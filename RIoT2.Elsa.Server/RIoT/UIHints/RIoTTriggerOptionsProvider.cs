using Elsa.Workflows;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Studio.Models;
using RIoT2.Elsa.Studio.UIProviders;
using System.Reflection;

namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    public class RIoTTriggerOptionsProvider(IRIoTDataService rIoTData) : IPropertyUIHandler
    {
        private readonly IRIoTDataService _rIoT = rIoTData;

        public float Priority { get; }

        public async ValueTask<IDictionary<string, object>> GetUIPropertiesAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken = default)
        {
            ICollection<RIoTTemplateItem> items = await GetItemsAsync(propertyInfo, context, cancellationToken);
            RIoTOutputProps value = new RIoTOutputProps
            {
                SelectList = new RIoTTemplateList(items),
                HideEditor = true
            };
            Dictionary<string, object> obj = new Dictionary<string, object> { ["riot-output-selector"] = value };
            return obj;
        }

        private async ValueTask<ICollection<RIoTTemplateItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
        {
            try
            {
                var selectListItems = new List<RIoTTemplateItem>();
                var reportTemplates = _rIoT.GetReportTemplatesAsync();
                var variableTemplates = _rIoT.GetVariableTemplatesAsync();

                await Task.WhenAll(variableTemplates, reportTemplates).WaitAsync(cancellationToken);

                addTemplatesTolist(selectListItems, await reportTemplates, TemplateType.Report);
                addTemplatesTolist(selectListItems, await variableTemplates, TemplateType.Variable);

                return selectListItems;
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching RIoT Templates: " + ex.Message, ex);
            }
        }

        private void addTemplatesTolist(List<RIoTTemplateItem> list, List<Template> templates, TemplateType type)
        {
            foreach (var t in templates)
            {
                list.Add(t.Create(type));
            }
        }
    }
}
