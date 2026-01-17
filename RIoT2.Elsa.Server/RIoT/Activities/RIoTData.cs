using Elsa.Extensions;
using Elsa.Studio.UIHints;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints.Dropdown;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Activity to get RIoT data object",
        DisplayName = "RIoT Data",
        Kind = ActivityKind.Action)]
    public class RIoTData : CodeActivity
    {
        private readonly IRIoTDataService _riot;

        public RIoTData(IRIoTDataService rIoTDataService) 
        {
            _riot = rIoTDataService;
        }

        [Input(
        Description = "Choose RIoT Data",
        UIHint = InputUIHints.DropDown,
        UIHandler = typeof(RIoTTriggerOptionsProvider)
        )]
        public Input<SelectListItem> SelectedDataSource { get; set; } = null!;

        [Output(Description = "RIoT data")]
        public Output<object> DataObject { get; set; } = default!;

        protected override void Execute(ActivityExecutionContext context)
        {
            var reportId = SelectedDataSource.Expression?.Value?.ToString() ?? "";

            if (!String.IsNullOrEmpty(reportId)) 
            {
                object data = _riot.GetReport(reportId);
                DataObject.Set(context, data);
            }
        }
    }
}
