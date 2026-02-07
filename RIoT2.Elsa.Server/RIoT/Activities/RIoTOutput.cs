using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
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

        protected override void Execute(ActivityExecutionContext context)
        {
            var cmd = Command.Get(context) ?? null;
            if (cmd != null && cmd.Id != null)
            {
                var riot = context.GetRequiredService<IRIoTDataService>();
                riot.ExecuteCommandAsync(cmd.Id, cmd.Value);
            }
        }
    }
}
