using Elsa.Extensions;
using Elsa.Studio.UIHints;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints.Dropdown;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;

namespace RIoT2.Elsa.Server.RIoT.Activities
{

    [Activity(
       Namespace = "RIoT2",
       Category = "RIoT",
       Description = "Activity to send commandto RIoT",
       DisplayName = "RIoT Output",
       Kind = ActivityKind.Task)]
    public class RIoTOutput : CodeActivity, ITerminalNode
    {
        private readonly IRIoTDataService _riot;

        public RIoTOutput(IRIoTDataService rIoTDataService)
        {
            _riot = rIoTDataService;
        }

        [Input(
            Description = "Choose RIoT Command",
            UIHint = InputUIHints.DropDown,
            UIHandler = typeof(RIoTTriggerOptionsProvider)
            )]
        public Input<SelectListItem> Command { get; set; } = null!;

        [Input(
           Description = "Data that will be sent to the Command",
           UIHint = InputUIHints.SingleLine
           )]
        public Input<object> CommandData { get; set; } = null!;

        protected override void Execute(ActivityExecutionContext context)
        {
            var commandId = Command.Expression?.Value?.ToString() ?? "";
            if (!String.IsNullOrEmpty(commandId))
            {
                object data = CommandData.Get(context) ?? new { };
                _riot.ExecuteCommand(commandId, data);
            }
        }
    }
}
