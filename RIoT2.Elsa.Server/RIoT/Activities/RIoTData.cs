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
            DefaultSyntax = "JavaScript",
            UIHandler = typeof(RIoTDataOptionsProvider)
            )]
        public Input<RIoTTemplateItem> SelectedDataSource { get; set; } = null!;

        [Output(Description = "RIoT data")]
        public Output<object> DataObject { get; set; } = default!;

        protected override void Execute(ActivityExecutionContext context)
        {
            var selection = SelectedDataSource.Get(context) ?? null;
            if (selection == null || selection.Id == null)
                return;

            var riot = context.GetRequiredService<IRIoTDataService>();
            object? data = null;

            switch (selection.TemplateType) 
            {
                case TemplateType.Report:
                    data = riot.GetReportValueAsync(selection.Id).Result;
                    break;
                case TemplateType.Variable:
                    data = riot.GetVariableValueAsync(selection.Id).Result;
                    break;
                case TemplateType.Command:
                    data = riot.GetCommandValueAsync(selection.Id).Result;
                    break;
                default:
                    break;
            }
            DataObject.Set(context, data);
        }
    }
}
