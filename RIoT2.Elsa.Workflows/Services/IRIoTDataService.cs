using RIoT2.Core.Models;
using RIoT2.Elsa.Workflows.Models;

namespace RIoT2.Elsa.Workflows.Services
{
    public interface IRIoTDataService
    {
        Task<object> GetReportValueAsync(string reportId);
        Task<object> GetCommandValueAsync(string commandId);
        Task<object> GetVariableValueAsync(string variableId);
        Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the command with <c>POST /api/v2/commands</c> and <c>"wait": true</c>, and returns its final
        /// delivery status, at the latest after <paramref name="ttlSeconds"/> (design 7.1).
        /// </summary>
        Task<CommandConfirmation> ExecuteCommandWithConfirmationAsync(string id, object? data, int ttlSeconds, CancellationToken cancellationToken = default);

        /// <summary>
        /// A window of the forecast report's latest series and its figures (ADR 0019, 2.1.0); <c>null</c> when the report has no
        /// series yet. Throws <see cref="ArgumentException"/> for an invalid window.
        /// </summary>
        Task<ForecastSummary?> GetForecastAsync(string reportId, ForecastRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This workflow host can't read forecasts.");

        /// <summary>Gives the orchestrator a command plan, which replaces the command's active plan (ADR 0019, 2.1.0).</summary>
        Task<CommandPlanReceipt> SubmitCommandPlanAsync(CommandPlanSubmission plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(CommandPlanReceipt.Failure("This workflow host can't plan commands."));

        /// <summary>Ends the command's active plan; the plan's id, or <c>null</c> when it had none (ADR 0019, 2.1.0).</summary>
        Task<string?> CancelCommandPlanAsync(string commandId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This workflow host can't plan commands.");
    }
}
