using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using RIoT2.Elsa.Workflows.Services;
using RIoT2.Elsa.Workflows.UIHints;
using RIoT2.Elsa.Workflows.Models;

namespace RIoT2.Elsa.Workflows.Activities
{
    /// <summary>
    /// Sends a notification through a RIoT2 command, normally a Messaging device's "Send firebase message" (push to the
    /// Mobile app, topic <c>alerts</c> or <c>notifications</c>) or "Send mail message". The value carries both forms:
    /// <c>title</c>, <c>body</c>, <c>topic</c>, <c>imgUrl</c>, and <c>subject</c> (the title) and <c>to</c>.
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Sends a push notification or mail through a Messaging device",
        DisplayName = "RIoT Notification",
        Kind = ActivityKind.Task)]
    public class RIoTNotification : CodeActivity
    {
        [Input(
            Description = "The Messaging command: Send firebase message or Send mail message",
            UIHint = "riot-output-selector",
            DefaultSyntax = "JavaScript",
            UIHandler = typeof(RIoTCommandOptionsProvider))]
        public Input<RIoTTemplateItem> Command { get; set; } = null!;

        [Input(Description = "The title (the mail's subject)")]
        public Input<string> Title { get; set; } = new("");

        [Input(Description = "The text")]
        public Input<string> Body { get; set; } = new("");

        [Input(Description = "Push: the Firebase topic, alerts or notifications", DefaultValue = "alerts")]
        public Input<string> Topic { get; set; } = new("alerts");

        [Input(Description = "Push, optional: the URL of an image")]
        public Input<string?> ImageUrl { get; set; } = default!;

        [Input(Description = "Mail: the recipient")]
        public Input<string?> To { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var command = Command.Get(context);
            if (command == null || string.IsNullOrWhiteSpace(command.Id))
                throw new InvalidOperationException("RIoT Notification requires a command.");
            var title = Title.GetOrDefault(context) ?? "";
            var body = Body.GetOrDefault(context) ?? "";
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
                throw new InvalidOperationException("RIoT Notification needs a title or a text.");
            var message = Message(title, body, Topic.GetOrDefault(context), ImageUrl?.GetOrDefault(context), To?.GetOrDefault(context));
            await context.GetRequiredService<IRIoTDataService>().ExecuteCommandAsync(command.Id, message, context.CancellationToken);
        }

        /// <summary>The command value: read as a Firebase message or as a mail by the Messaging device.</summary>
        public static Dictionary<string, object?> Message(string title, string body, string? topic, string? imageUrl, string? to)
        {
            var message = new Dictionary<string, object?>
            {
                ["title"] = title,
                ["body"] = body,
                ["topic"] = string.IsNullOrWhiteSpace(topic) ? "alerts" : topic.Trim(),
                ["subject"] = title
            };
            if (!string.IsNullOrWhiteSpace(imageUrl))
                message["imgUrl"] = imageUrl.Trim();
            if (!string.IsNullOrWhiteSpace(to))
                message["to"] = to.Trim();
            return message;
        }
    }
}
