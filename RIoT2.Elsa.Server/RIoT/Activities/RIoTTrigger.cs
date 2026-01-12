using Cronos;
using Elsa.Expressions.Models;
using Elsa.Extensions;
using Elsa.Http;
using Elsa.Http.Bookmarks;
using Elsa.Http.Extensions;
using Elsa.Http.UIHints;
using Elsa.Scheduling;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Memory;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using RIoT2.Elsa.Server.RIoT.UIHints;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    [Activity(
        Namespace = "RIoT2.Elsa.Server.RIoT",
        Category = "RIoT",
        Description = "Triggers a workflow when a specified RIoT event occurs.",
        DisplayName = "RIoT Trigger",
        Kind = ActivityKind.Trigger)]
    [Output(IsSerializable = true)]
    public class RIoTTrigger : Trigger<object?>
    {
        public const string EventInputWorkflowInputKey = "__RIoTTriggerPayloadWorkflowInput";

        /// <inheritdoc />
        internal RIoTTrigger([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
        {
        }

        /// <inheritdoc />
        public RIoTTrigger(string eventName, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null)
            : this(new Literal<string>(eventName), source, line)
        {
        }

        /// <inheritdoc />
        public RIoTTrigger(Func<string> eventName, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null)
            : this(new Input<string>(Expression.DelegateExpression(eventName), new()), source, line)
        {
        }

        /// <inheritdoc />
        public RIoTTrigger(Func<ExpressionExecutionContext, string?> eventName, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null)
            : this(new Input<string>(Expression.DelegateExpression(eventName), new()), source, line)
        {
        }

        /// <inheritdoc />
        public RIoTTrigger(Variable<string> variable, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(source, line)
        {
            EventName = new(variable);
        }

        /// <inheritdoc />
        public RIoTTrigger(Literal<string> literal, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(source, line)
        {
            EventName = new(literal);
        }

        /// <inheritdoc />
        public RIoTTrigger(Input<string> eventName, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(source, line)
        {
            EventName = eventName;
        }

        /// <summary>
        /// The name of the event to listen for.
        /// </summary>
        [Input(Description = "The name of the event to listen for.")]
        public Input<string> EventName { get; set; } = null!;

        [Input(
        Description = "Choose RIoT trigger from the list",
        UIHint = InputUIHints.DropDown,
        UIHandler = typeof(RIoTTriggerOptionsProvider)
        )]
        public Input<string> SelectedTrigger { get; set; } = default!;

        /// <inheritdoc />
        protected override object GetTriggerPayload(TriggerIndexingContext context)
        {
            var eventName = EventName.Get(context.ExpressionExecutionContext);
            return context.GetEventStimulus(eventName);
        }

        /// <inheritdoc />
        protected override void Execute(ActivityExecutionContext context)
        {
            var eventName = context.Get(EventName)!;
            context.WaitForEvent(eventName, CompleteInternalAsync);
        }

        private async ValueTask CompleteInternalAsync(ActivityExecutionContext context)
        {
            context.SetResult(context.GetEventInput());
            await context.CompleteActivityAsync();
        }
    }
}
