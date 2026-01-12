using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.UIHints;
using Elsa.Workflows.UIHints.Dropdown;
using System.Reflection;

namespace RIoT2.Elsa.Server.RIoT.UIHints
{
    public class RIoTTriggerOptionsProvider() : DropDownOptionsProviderBase
    {
        /// <inheritdoc />
        protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
        {
            var selectListItems = new List<SelectListItem> { new("Option0", "Value0") };

            selectListItems.Add(new SelectListItem("Option1", "Value1"));
            selectListItems.Add(new SelectListItem("Option2", "Value2"));
            selectListItems.Add(new SelectListItem("Option3", "Value3"));

            return new(selectListItems);
        }
    }
}
