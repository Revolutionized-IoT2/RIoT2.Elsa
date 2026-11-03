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
    }
}
