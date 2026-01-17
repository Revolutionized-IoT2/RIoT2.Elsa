using Acornima.Ast;
using Elsa.Api.Client.Shared.UIHints.DropDown;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using RIoT2.Elsa.Server.RIoT.UIHints;
using System.Text;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Triggers a workflow when a specified RIoT event occurs.",
        DisplayName = "RIoT Trigger",
        Kind = ActivityKind.Trigger)]
    public class RIoTTrigger : Trigger, IStartNode
    {
        [Input(
        Description = "Choose RIoT trigger from the list",
        UIHint = InputUIHints.DropDown,
        UIHandler = typeof(RIoTTriggerOptionsProvider)
        )]
        public Input<SelectListItem> SelectedTrigger { get; set; } = null!;

        [Output(Description = "Received trigger data")]
        public Output<object> EventData { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            // Check if this trigger started the workflow
            if (context.IsTriggerOfWorkflow())
            {
                var eventData = context.WorkflowInput.GetValueOrDefault<object>("EventData");
                EventData.Set(context, eventData);
                await context.CompleteActivityAsync();
                return; 
            }

            //string optionValue = SelectedTrigger.Expression?.Value?.ToString() ?? "DefaultOption";
        }

        protected override object GetTriggerPayload(TriggerIndexingContext context)
        {
            // Return payload used to match incoming events to workflows
            var eventName = SelectedTrigger.Expression?.Value?.ToString() ?? "DefaultOption";
            return eventName ?? "DefaultEvent";
        }
    }
}
