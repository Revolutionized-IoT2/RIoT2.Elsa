using Elsa.Expressions.JavaScript.Contracts;
using Elsa.Expressions.Models;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Jint;
using RIoT2.Elsa.Workflows.Services;
using RIoT2.Elsa.Workflows.UIHints;
using RIoT2.Elsa.Workflows.Models;

namespace RIoT2.Elsa.Workflows.Activities
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

            var result = await EvaluateValueAsync(context, cmd);
            var riot = context.GetRequiredService<IRIoTDataService>();
            await riot.ExecuteCommandAsync(cmd.Id, result, context.CancellationToken);
        }

        /// <summary>Runs the command's value expression (JavaScript), as RIoT Output and RIoT Command with confirmation do.</summary>
        internal static async Task<object?> EvaluateValueAsync(ActivityExecutionContext context, RIoTTemplateItem cmd)
        {
            var script = cmd.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(script))
                throw new InvalidOperationException("The command needs a value expression.");

            var javaScriptEvaluator = context.GetRequiredService<IJavaScriptEvaluator>();
            return await javaScriptEvaluator.EvaluateAsync(
                script,
                typeof(object),
                context.ExpressionExecutionContext,
                ExpressionEvaluatorOptions.Empty,
                engine => ConfigureEngine(engine, context),
                context.CancellationToken);
        }

        private static void ConfigureEngine(Engine engine, ActivityExecutionContext context)
        {
            engine.SetValue("setOutcome", (Action<string>)(value => context.TransientProperties["Outcomes"] = new[] { value }));
            engine.SetValue("setOutcomes", (Action<string[]>)(value => context.TransientProperties["Outcomes"] = value));
        }
    }
}
