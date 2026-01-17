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
        private readonly IRIoTDataService _rIoTData = rIoTData;
        /// <inheritdoc />
        protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
        {
            /*
            try
            {
                var selectListItems = new List<SelectListItem>();
                foreach (var template in _riot.GetReportTemplates())
                    selectListItems.Add(new SelectListItem(template.Name ?? "-Unnamed-", template.Id ?? "-NoId-"));

                return new(selectListItems);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching RIoT Templates: " + ex.Message, ex);
            }*/


            var selectListItems = new List<SelectListItem> { new("Option0", "test") };

            selectListItems.Add(new SelectListItem("Option1", "test1"));
            selectListItems.Add(new SelectListItem("Option2", "test2"));
            selectListItems.Add(new SelectListItem("Option3", "test3"));

            return new(selectListItems);
        }
    }
}
