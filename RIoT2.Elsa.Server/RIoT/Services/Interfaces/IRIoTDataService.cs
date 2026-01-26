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
        Task ExecuteCommandAsync(string id, object data);
    }
}
