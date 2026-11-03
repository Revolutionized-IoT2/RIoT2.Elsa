using Elsa.Workflows;

namespace RIoT2.Elsa.Workflows.Activities
{
    /// <summary>What the automation activities share.</summary>
    public static class AutomationActivity
    {
        /// <summary>The workflow input with the report that started (or resumed) the workflow; set by <c>RIoT Trigger</c>'s stimulus.</summary>
        public const string EventDataInput = "EventData";

        /// <summary>
        /// The key of the state shared between runs: the given one, or the workflow definition and the activity, so every
        /// copy of an activity in a definition has its own state.
        /// </summary>
        public static string Key(ActivityExecutionContext context, string? key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                return key.Trim();
            var definition = context.WorkflowExecutionContext.Workflow.Identity.DefinitionId;
            return $"{definition}/{context.Activity.Id}";
        }

        /// <summary>The report that started or resumed the workflow, or <c>null</c>.</summary>
        public static object? EventData(ActivityExecutionContext context) =>
            context.WorkflowInput.TryGetValue(EventDataInput, out var data) ? data : null;
    }
}
