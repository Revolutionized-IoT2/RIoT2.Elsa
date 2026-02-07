using Elsa.Workflows;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Studio.Models;
using RIoT2.Elsa.Studio.UIProviders;
using System.Reflection;
using System.Text.Json;

namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    public class RIoTOutputOptionsProvider(IRIoTDataService rIoTData) : IPropertyUIHandler
    {
        private readonly IRIoTDataService _rIoT = rIoTData;

        public float Priority { get; }

        public ValueTask<IDictionary<string, object>> GetUIPropertiesAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken = default)
        {
            ICollection<RIoTTemplateItem> items = GetItemsAsync(propertyInfo, context, cancellationToken).Result;
            RIoTOutputProps value = new RIoTOutputProps
            {
                SelectList = new RIoTTemplateList(items)
            };
            Dictionary<string, object> obj = new Dictionary<string, object> { ["riot-output-selector"] = value };
            return new(obj);
        }

        /// <inheritdoc />
        private ValueTask<ICollection<RIoTTemplateItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
        {
            try
            {
                var selectListItems = new List<RIoTTemplateItem>();
                var commandTemplates = _rIoT.GetCommandTemplatesAsync().Result;

                foreach (var t in commandTemplates) 
                {
                    //var name = $"{t.Name} [{t.Node}:{t.Device}] [{t.Type}]";
                    selectListItems.Add(new RIoTTemplateItem
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Type = t.Type,
                        Node = t.Node,
                        Device = t.Device,
                        Model = t.Model
                    });
                }

                return new(selectListItems);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching RIoT Templates: " + ex.Message, ex);
            }
        }
    }
}
