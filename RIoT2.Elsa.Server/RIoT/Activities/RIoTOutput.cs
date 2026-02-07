using Elsa.Expressions.Models;
using Elsa.Extensions;
using Elsa.JavaScript.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Jint;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;
using RIoT2.Elsa.Studio.Models;

namespace RIoT2.Elsa.Server.RIoT.Activities
{

    [Activity(
       Namespace = "RIoT2",
       Category = "RIoT",
       Description = "Activity to send command to RIoT",
       DisplayName = "RIoT Output",
       Kind = ActivityKind.Task)]
    public class RIoTOutput : CodeActivity, ITerminalNode
    {
        [Input(
            Description = "Choose RIoT Command to Execute",
            UIHint = "riot-output-selector",
            UIHandler = typeof(RIoTOutputOptionsProvider)
            )]
        public Input<RIoTTemplateItem> Command { get; set; } = null!;

        protected override async void Execute(ActivityExecutionContext context)
        {
            var cmd = Command.Get(context) ?? null;
            if (cmd == null || cmd.Id == null)
                return;

            var script = cmd.Value ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(script))
                return;

            var javaScriptEvaluator = context.GetRequiredService<IJavaScriptEvaluator>();

            var result = await javaScriptEvaluator.EvaluateAsync(
                script,
                typeof(object),
                context.ExpressionExecutionContext,
                ExpressionEvaluatorOptions.Empty,
                engine => ConfigureEngine(engine, context),
                context.CancellationToken);

                var riot = context.GetRequiredService<IRIoTDataService>();
                await riot.ExecuteCommandAsync(cmd.Id, result);
        }

        private static void ConfigureEngine(Engine engine, ActivityExecutionContext context)
        {
            engine.SetValue("setOutcome", (Action<string>)(value => context.TransientProperties["Outcomes"] = new[] { value }));
            engine.SetValue("setOutcomes", (Action<string[]>)(value => context.TransientProperties["Outcomes"] = value));
        }
    }
}
