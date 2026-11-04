using System.Globalization;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using RIoT2.Core.Models;
using RIoT2.Elsa.Workflows.Automation;
using RIoT2.Elsa.Workflows.Models;
using RIoT2.Elsa.Workflows.Services;
using RIoT2.Elsa.Workflows.UIHints;

namespace RIoT2.Elsa.Workflows.Activities
{
    /// <summary>
    /// Reads a window of a forecast report (ADR 0019): a weather forecast, day-ahead prices or any report whose value is a
    /// forecast series. Gives the window's figures and points to the next steps, and continues on <c>Done</c>, or on
    /// <c>Unavailable</c> when there is no forecast yet or (with <c>RequireComplete</c>) it doesn't cover the whole window,
    /// as tomorrow's prices before they are published.
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Reads a forecast, such as tomorrow's temperatures or prices: minimum, maximum, average, the cheapest or warmest hours",
        DisplayName = "RIoT Forecast",
        Kind = ActivityKind.Task)]
    [FlowNode(DoneOutcome, UnavailableOutcome)]
    public class RIoTForecast : Activity
    {
        public const string DoneOutcome = "Done";
        public const string UnavailableOutcome = "Unavailable";

        [Input(
            Description = "The forecast report",
            UIHint = RIoTSelectorUIHandler.UIHint,
            DefaultSyntax = "JavaScript",
            UIHandler = typeof(RIoTForecastOptionsProvider))]
        public Input<RIoTTemplateItem> Forecast { get; set; } = null!;

        [Input(Description = "all: the whole forecast; today or tomorrow: the local day; next: the next hours; range: From until To",
            UIHint = InputUIHints.DropDown, Options = new[] { ForecastWindows.Next, ForecastWindows.Today, ForecastWindows.Tomorrow, ForecastWindows.All, ForecastWindows.Range },
            DefaultValue = ForecastWindows.Next)]
        public Input<string> Window { get; set; } = new(ForecastWindows.Next);

        [Input(Description = "With next: how many hours, 1-384", DefaultValue = ForecastWindow.DefaultHours)]
        public Input<int> Hours { get; set; } = new(ForecastWindow.DefaultHours);

        [Input(Description = "With range: the start, an ISO 8601 time such as 2026-11-05T06:00:00Z")]
        public Input<string?> From { get; set; } = default!;

        [Input(Description = "With range: the end, an ISO 8601 time")]
        public Input<string?> To { get; set; } = default!;

        [Input(Description = "How many of the lowest and highest points to give, 0-100; for example the 4 cheapest hours", DefaultValue = 0)]
        public Input<int> Rank { get; set; } = new(0);

        [Input(Description = "Continue on Unavailable unless the forecast covers the whole window", DefaultValue = true)]
        public Input<bool> RequireComplete { get; set; } = new(true);

        [Output(Description = "The lowest value in the window")]
        public Output<double?> Min { get; set; } = default!;

        [Output(Description = "The highest value in the window")]
        public Output<double?> Max { get; set; } = default!;

        [Output(Description = "The average over the window, weighted by time")]
        public Output<double?> Average { get; set; } = default!;

        [Output(Description = "The sum of the values, such as the rain in millimetres")]
        public Output<double?> Sum { get; set; } = default!;

        [Output(Description = "The value that applies now, when now is in the window")]
        public Output<double?> Current { get; set; } = default!;

        [Output(Description = "Whether the forecast covers the whole window")]
        public Output<bool> Complete { get; set; } = default!;

        [Output(Description = "The points in the window: a list of { start, end, value }")]
        public Output<object?> Points { get; set; } = default!;

        [Output(Description = "The Rank lowest points, lowest first")]
        public Output<object?> Lowest { get; set; } = default!;

        [Output(Description = "The Rank highest points, highest first")]
        public Output<object?> Highest { get; set; } = default!;

        [Output(Description = "Everything above as one object, for example for Run LLM's input")]
        public Output<object?> Summary { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var selection = Forecast.Get(context);
            if (selection == null || string.IsNullOrWhiteSpace(selection.Id))
                throw new InvalidOperationException("RIoT Forecast needs a forecast report.");
            var rank = Rank.GetOrDefault(context);
            if (rank is < 0 or > ForecastWindow.MaxRank)
                throw new InvalidOperationException($"The rank must be 0-{ForecastWindow.MaxRank}, not {rank}.");

            var request = new ForecastRequest
            {
                Window = (Window.GetOrDefault(context) ?? ForecastWindows.Next).Trim(),
                Hours = Hours.GetOrDefault(context),
                From = Time(From.GetOrDefault(context), "From"),
                To = Time(To.GetOrDefault(context), "To"),
                Rank = rank
            };
            var riot = context.GetRequiredService<IRIoTDataService>();
            ForecastSummary? summary;
            try
            {
                summary = await riot.GetForecastAsync(selection.Id, request, context.CancellationToken);
            }
            catch (ArgumentException x)
            {
                throw new InvalidOperationException($"RIoT Forecast: {x.Message}", x);
            }

            if (summary == null)
            {
                context.GetRequiredService<ILogger<RIoTForecast>>().LogInformation("RIoT Forecast {Activity}: {Report} has no forecast yet", context.Activity.Id, selection.Id);
                Complete.Set(context, false);
                await context.CompleteActivityWithOutcomesAsync(UnavailableOutcome);
                return;
            }

            Min.Set(context, summary.Min);
            Max.Set(context, summary.Max);
            Average.Set(context, summary.Average);
            Sum.Set(context, summary.Sum);
            Current.Set(context, summary.Current?.Value);
            Complete.Set(context, summary.Complete);
            var plain = ActivityJson.ToObject(System.Text.Json.Nodes.JsonNode.Parse(RIoT2.Core.Utils.Json.Serialize(summary)!)) as IDictionary<string, object?>;
            Points.Set(context, plain?["points"]);
            Lowest.Set(context, plain?["lowest"]);
            Highest.Set(context, plain?["highest"]);
            Summary.Set(context, plain);

            var available = summary.Count > 0 && (summary.Complete || !RequireComplete.GetOrDefault(context));
            await context.CompleteActivityWithOutcomesAsync(available ? DoneOutcome : UnavailableOutcome);
        }

        private static DateTimeOffset? Time(string? text, string input)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time))
                return time;
            throw new InvalidOperationException($"{input} is not a time: '{text}'. Use ISO 8601, such as 2026-11-05T06:00:00Z.");
        }
    }
}
