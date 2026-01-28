using Elsa.Workflows.UIHints.Dropdown;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using System.Reflection;

namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    public class RIoTDataOptionsProvider(IRIoTDataService rIoTData) : DropDownOptionsProviderBase
    {
        //TODO create custom UI element to show more details about each template
        private readonly IRIoTDataService _rIoT = rIoTData;
        /// <inheritdoc />
        protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
        {
            try
            {
                var selectListItems = new List<SelectListItem>();
                var reportTemplates = _rIoT.GetReportTemplatesAsync();
                var variableTemplates = _rIoT.GetVariableTemplatesAsync();
                var commandTemplates = _rIoT.GetCommandTemplatesAsync();

                Task.WaitAll(reportTemplates, variableTemplates, commandTemplates);

                foreach (var t in reportTemplates.Result) 
                {
                    var name = $"{t.Name} [{t.Node}:{t.Device}] [{t.Type}]";
                    selectListItems.Add(new SelectListItem(name, t.Id ?? "--"));
                }

                foreach (var t in variableTemplates.Result) 
                {
                    var name = $"{t.Name} [{t.Node}:{t.Device}] [{t.Type}]";
                    selectListItems.Add(new SelectListItem(name, t.Id ?? "--"));
                }

                foreach (var t in commandTemplates.Result)
                {
                    var name = $"{t.Name} [{t.Node}:{t.Device}] [{t.Type}]";
                    selectListItems.Add(new SelectListItem(name, t.Id ?? "--"));
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
