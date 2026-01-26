using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.UIHints;
using Elsa.Workflows.UIHints.Dropdown;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;
using System.Reflection;

namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    public class RIoTTriggerOptionsProvider(IRIoTDataService rIoTData) : DropDownOptionsProviderBase
    {
        private readonly IRIoTDataService _rIoT = rIoTData;
        /// <inheritdoc />
        protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
        {
            try
            {
                var selectListItems = new List<SelectListItem>();
                var reportTemplates = _rIoT.GetReportTemplatesAsync();
                var variableTemplates = _rIoT.GetVariableTemplatesAsync();
                
                Task.WaitAll(reportTemplates, variableTemplates);

                foreach (var t in reportTemplates.Result)
                    selectListItems.Add(new SelectListItem(t.Name ?? "-unknown report-", t.Id ?? "--"));

                foreach (var t in variableTemplates.Result)
                    selectListItems.Add(new SelectListItem(t.Name ?? "-unknown variable-", t.Id ?? "--"));

                return new(selectListItems);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching RIoT Templates: " + ex.Message, ex);
            }
        }
    }
}
