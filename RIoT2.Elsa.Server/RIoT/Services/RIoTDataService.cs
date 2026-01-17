using RIoT2.Core.Utils;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    public class RIoTDataService : IRIoTDataService
    {
        private readonly IRIoTConfigurationService _configuration;

        public RIoTDataService(IRIoTConfigurationService configurationService)
        {
            _configuration = configurationService;
        }
    
        public List<ReportTemplate> GetReportTemplates()
        {
            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = _configuration.OrchestratorBaseUrl + $"/api/nodes/report/templates";
                var response = Web.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    var json = response.Content.ReadAsStringAsync().Result;
                    return Json.Deserialize<List<ReportTemplate>>(json);
                }
            }
            return new List<ReportTemplate>();
        }
    }
}
