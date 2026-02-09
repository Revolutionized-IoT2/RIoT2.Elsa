using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using RIoT2.Elsa.Server.RIoT.UIHints;
using RIoT2.Elsa.Studio.Models;

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
           UIHint = "riot-output-selector",
           DefaultSyntax = "JavaScript",
           UIHandler = typeof(RIoTTriggerOptionsProvider)
           )]
        public Input<RIoTTemplateItem> SelectedTrigger { get; set; } = null!;

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
            return;
        }

        protected override object GetTriggerPayload(TriggerIndexingContext context)
        {
            var eventname = "";
            var selection = SelectedTrigger.Get(context.ExpressionExecutionContext) ?? null;
            if (selection != null)
                eventname = selection.Id ?? "DefaultEvent";

            return eventname;
        }
    }
}
