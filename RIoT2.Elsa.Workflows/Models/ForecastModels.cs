using RIoT2.Core.Models;

namespace RIoT2.Elsa.Workflows.Models
{
    /// <summary>
    /// Which part of a forecast <c>RIoT Forecast</c> asks for (ADR 0019): a <see cref="ForecastWindows"/> window, the hours of
    /// <c>next</c>, the times of <c>range</c>, and how many lowest and highest points to rank.
    /// </summary>
    public sealed record ForecastRequest
    {
        public string Window { get; init; } = ForecastWindows.Next;
        public int? Hours { get; init; }
        public DateTimeOffset? From { get; init; }
        public DateTimeOffset? To { get; init; }
        public int Rank { get; init; }
    }

    /// <summary>One step of a command plan: the command's value from <see cref="At"/> on.</summary>
    public sealed record CommandPlanStepInput(DateTimeOffset At, object? Value);

    /// <summary>
    /// A plan of future values for one command (ADR 0019): the orchestrator sends each step's value at its time, and a new
    /// plan for the command replaces the old one.
    /// </summary>
    public sealed record CommandPlanSubmission
    {
        public required string CommandId { get; init; }
        public string? Name { get; init; }

        /// <summary>Who made the plan, such as the workflow; shown with the plan.</summary>
        public string? Source { get; init; }

        /// <summary><c>latest</c> (default): after a pause, the latest missed step is sent; <c>none</c>: missed steps are skipped.</summary>
        public string? CatchUp { get; init; }

        public IReadOnlyList<CommandPlanStepInput> Steps { get; init; } = [];
    }

    /// <summary>What the orchestrator answered to a plan.</summary>
    public sealed record CommandPlanReceipt
    {
        public bool Succeeded { get; init; }
        public string? PlanId { get; init; }
        public int StepCount { get; init; }
        public string? Error { get; init; }

        public static CommandPlanReceipt Failure(string error) => new() { Succeeded = false, Error = error };
    }
}
