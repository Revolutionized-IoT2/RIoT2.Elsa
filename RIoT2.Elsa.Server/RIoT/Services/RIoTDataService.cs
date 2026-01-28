using RIoT2.Core.Models;
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

        public async Task ExecuteCommandAsync(string id, object data)
        {
            Command c = new Command
            {
                Id = id,
                Value = new ValueModel(data)
            };

            await Web.PostAsync(_configuration.OrchestratorBaseUrl + $"/api/command/execute", Json.Serialize(c));
        }

        public async Task<List<Template>> GetCommandTemplatesAsync()
        {
            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = _configuration.OrchestratorBaseUrl + $"/api/command/templates";
                var response = await Web.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return Json.Deserialize<List<Template>>(json);
                }
            }
            return [];
        }

        /// <summary>
        /// This method retrieves the current value of a report by its ID.
        /// </summary>
        /// <param name="reportId"></param>
        /// <returns></returns>
        public async Task<object> GetReportValueAsync(string reportId)
        {
            var r = new ElsaReport
            {
                Id = reportId
            };

            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = _configuration.OrchestratorBaseUrl + $"/api/report/{reportId}/value";
                var response = await Web.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    r = Json.Deserialize<ElsaReport>(json);
                }
            }
            return r;
        }

        /// <summary>
        /// This method retrieves the current value of a command by its ID.
        /// </summary>
        /// <param name="commandId"></param>
        /// <returns></returns>
        public async Task<object> GetCommandValueAsync(string commandId)
        {
            var c = new Command { Id = commandId };

            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = _configuration.OrchestratorBaseUrl + $"/api/command/{commandId}/value";
                var response = await Web.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    c = Json.Deserialize<Command>(json);
                }
            }
            return c;
        }

        public async Task<List<Template>> GetReportTemplatesAsync()
        {
            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = _configuration.OrchestratorBaseUrl + $"/api/report/templates";
                var response = await Web.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return Json.Deserialize<List<Template>>(json);
                }
            }
            return [];
        }

        public async Task<List<Template>> GetVariableTemplatesAsync()
        {
            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = _configuration.OrchestratorBaseUrl + $"/api/variable/templates";
                var response = await Web.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return Json.Deserialize<List<Template>>(json);
                }
            }
            return [];
        }

        public async Task<object> GetVariableValueAsync(string variableId)
        {
            var v = new Variable { Id = variableId };

            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = _configuration.OrchestratorBaseUrl + $"/api/variable/{variableId}/value";
                var response = await Web.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    v = Json.Deserialize<Variable>(json);
                }
            }
            return v;
        }
    }
}
