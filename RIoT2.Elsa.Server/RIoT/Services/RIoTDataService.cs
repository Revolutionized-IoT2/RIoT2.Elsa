using RIoT2.Core.Models;
using RIoT2.Core.Utils;
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

        public void ExecuteCommand(string id, object data)
        {
            throw new NotImplementedException();
        }

        public List<CommandTemplate> GetCommandTemplates()
        {
            throw new NotImplementedException();
        }

        public object GetReport(string reportId)
        {
            return new
            {
                Value = 42
            }; 
        }

        /*
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
        }*/

        //For testing
        public List<ReportTemplate> GetReportTemplates()
        {
            return new List<ReportTemplate>()
            {
                new ReportTemplate
                {
                    Name = "XXX Report",
                    Id = "report-789",
                    Type = Core.ValueType.Text,
                    Address = "sensor/temperature",
                    MaintainHistory = true,
                },
                new ReportTemplate
                {
                    Name = "Temperature",
                    Id = "report-333",
                    Type = Core.ValueType.Text,
                    Address = "sensor/temperature",
                    MaintainHistory = true,
                },
            };
        }
    }
}
