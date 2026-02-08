using Elsa.Extensions;
using Elsa.Studio.UIHints;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints.Dropdown;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;
using RIoT2.Elsa.Studio.Models;

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
        [Input(
            Description = "Choose RIoT Data",
            UIHint = "riot-output-selector",
            UIHandler = typeof(RIoTDataOptionsProvider)
            )]
        public Input<RIoTTemplateItem> SelectedDataSource { get; set; } = null!;

        [Output(Description = "RIoT data")]
        public Output<object> DataObject { get; set; } = default!;

        protected override void Execute(ActivityExecutionContext context)
        {
            var reportId = SelectedDataSource.Expression?.Value?.ToString() ?? "";

            if (!String.IsNullOrEmpty(reportId)) 
            {
                //TODO get variables and CMD's also
                var riot = context.GetRequiredService<IRIoTDataService>();
                object data = riot.GetReportValueAsync(reportId).Result;
                DataObject.Set(context, data);
            }
        }
    }
}
