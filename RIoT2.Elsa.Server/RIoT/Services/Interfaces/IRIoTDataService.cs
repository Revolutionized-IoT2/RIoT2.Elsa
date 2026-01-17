using RIoT2.Elsa.Server.RIoT.Models;

namespace RIoT2.Elsa.Server.RIoT.Services.Interfaces
{
    public interface IRIoTDataService
    {
        List<ReportTemplate> GetReportTemplates();
    }
}
