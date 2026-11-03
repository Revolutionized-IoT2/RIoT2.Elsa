using RIoT2.Elsa.Server.RIoT.Models;

namespace RIoT2.Elsa.Server.RIoT.Services.Interfaces
{
    public interface IRIoTDataService
    {
        Task<List<Template>> GetReportTemplatesAsync();
        Task<List<Template>> GetCommandTemplatesAsync();
        Task<List<Template>> GetVariableTemplatesAsync();
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
