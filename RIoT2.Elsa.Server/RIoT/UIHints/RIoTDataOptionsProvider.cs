using Elsa.Workflows;
using Elsa.Workflows.UIHints.Dropdown;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Studio.Models;
using RIoT2.Elsa.Studio.UIProviders;
using System.Reflection;

namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    public class RIoTDataOptionsProvider(IRIoTDataService rIoTData) : IPropertyUIHandler
    {
        private readonly IRIoTDataService _rIoT = rIoTData;

        public float Priority { get; }

        public ValueTask<IDictionary<string, object>> GetUIPropertiesAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken = default)
        {
            ICollection<RIoTTemplateItem> items = GetItemsAsync(propertyInfo, context, cancellationToken).Result;
            RIoTOutputProps value = new RIoTOutputProps
            {
                SelectList = new RIoTTemplateList(items),
                HideEditor = true // Hide the editor. We only want to add value default model to JSEditor
            };
            Dictionary<string, object> obj = new Dictionary<string, object> { ["riot-output-selector"] = value };
            return new(obj);
        }

        private ValueTask<ICollection<RIoTTemplateItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
        {
            try
            {
                var selectListItems = new List<RIoTTemplateItem>();
                var reportTemplates = _rIoT.GetReportTemplatesAsync();
                var variableTemplates = _rIoT.GetVariableTemplatesAsync();
                var commandTemplates = _rIoT.GetCommandTemplatesAsync();

                Task.WaitAll(reportTemplates, variableTemplates, commandTemplates);

                addTemplatesTolist(selectListItems, commandTemplates.Result);
                addTemplatesTolist(selectListItems, variableTemplates.Result);
                addTemplatesTolist(selectListItems, reportTemplates.Result);

                return new(selectListItems);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching RIoT Templates: " + ex.Message, ex);
            }
        }

        private void addTemplatesTolist(List<RIoTTemplateItem> list, List<Template> templates)
        {
            foreach (var t in templates)
            {
                list.Add(new RIoTTemplateItem
                {
                    Id = t.Id,
                    Name = t.Name,
                    Type = t.Type,
                    Node = t.Node,
                    Device = t.Device,
                    Model = t.Model
                });
            }
        }
    }
}
