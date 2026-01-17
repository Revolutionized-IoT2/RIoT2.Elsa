using RIoT2.Core.Models;

namespace RIoT2.Elsa.Server.RIoT.Services.Interfaces
{
    public interface IRIoTDataService
    {
        List<ReportTemplate> GetReportTemplates();
        List<CommandTemplate> GetCommandTemplates();
        object GetReport(string reportId);
        void ExecuteCommand(string id, object data);
    }
}
