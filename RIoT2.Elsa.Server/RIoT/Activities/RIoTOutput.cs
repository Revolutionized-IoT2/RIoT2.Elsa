using Elsa.Expressions.JavaScript.Contracts;
using Elsa.Expressions.Models;
using Elsa.Extensions;
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
            Description = "Define RIoT Command to Execute",
            UIHint = "riot-output-selector",
            DefaultSyntax = "JavaScript",
            UIHandler = typeof(RIoTOutputOptionsProvider)
            )]
        public Input<RIoTTemplateItem> Command { get; set; } = null!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var cmd = Command.Get(context) ?? null;
            if (cmd == null || string.IsNullOrWhiteSpace(cmd.Id))
                throw new InvalidOperationException("RIoT Output requires a command identifier.");

            var script = cmd.Value ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(script))
                throw new InvalidOperationException("RIoT Output requires a command value expression.");

            var javaScriptEvaluator = context.GetRequiredService<IJavaScriptEvaluator>();

            var result = await javaScriptEvaluator.EvaluateAsync(
                script,
                typeof(object),
                context.ExpressionExecutionContext,
                ExpressionEvaluatorOptions.Empty,
                engine => ConfigureEngine(engine, context),
                context.CancellationToken);

            var riot = context.GetRequiredService<IRIoTDataService>();
            await riot.ExecuteCommandAsync(cmd.Id, result, context.CancellationToken);
        }

        private static void ConfigureEngine(Engine engine, ActivityExecutionContext context)
        {
            engine.SetValue("setOutcome", (Action<string>)(value => context.TransientProperties["Outcomes"] = new[] { value }));
            engine.SetValue("setOutcomes", (Action<string[]>)(value => context.TransientProperties["Outcomes"] = value));
        }
    }
}
