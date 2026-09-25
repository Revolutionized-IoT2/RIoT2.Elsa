using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    public class RIoTDataService : IRIoTDataService
    {
        private readonly IRIoTConfigurationService _configuration;
        private readonly HttpClient _httpClient;

        public RIoTDataService(IRIoTConfigurationService configurationService, HttpClient httpClient)
        {
            _configuration = configurationService;
            _httpClient = httpClient;
        }

        public async Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A command identifier is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(_configuration.OrchestratorBaseUrl))
                throw new InvalidOperationException("The orchestrator URL has not been configured.");

            Command c = new Command
            {
                Id = id,
                Value = new ValueModel(data)
            };

            using var body = new StringContent(Json.Serialize(c), System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(
                _configuration.OrchestratorBaseUrl.TrimEnd('/') + "/api/command/execute", body, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<Template>> GetCommandTemplatesAsync()
        {
            return await GetTemplateListAsync("/api/command/templates");
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
                var url = BuildUrl($"/api/report/{Uri.EscapeDataString(reportId)}/value");
                using var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    r = Json.Deserialize<ElsaReport>(json) ?? r;
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
                var url = BuildUrl($"/api/command/{Uri.EscapeDataString(commandId)}/value");
                using var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    c = Json.Deserialize<Command>(json) ?? c;
                }
            }
            return c;
        }

        public async Task<List<Template>> GetReportTemplatesAsync()
        {
            return await GetTemplateListAsync("/api/report/templates");
        }

        public async Task<List<Template>> GetVariableTemplatesAsync()
        {
            return await GetTemplateListAsync("/api/variable/templates");
        }

        public async Task<object> GetVariableValueAsync(string variableId)
        {
            var v = new Variable { Id = variableId };

            if (!String.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
            {
                var url = BuildUrl($"/api/variable/{Uri.EscapeDataString(variableId)}/value");
                using var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    v = Json.Deserialize<Variable>(json) ?? v;
                }
            }
            return v;
        }

        private async Task<List<Template>> GetTemplateListAsync(string path)
        {
            if (string.IsNullOrEmpty(_configuration.OrchestratorBaseUrl))
                return [];

            using var response = await _httpClient.GetAsync(BuildUrl(path));
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(json))
                return [];

            return JsonSerializer.Deserialize<List<Template>>(json, serializerOptions) ?? [];
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(_configuration.OrchestratorBaseUrl))
                throw new InvalidOperationException("The orchestrator URL has not been configured.");

            return _configuration.OrchestratorBaseUrl.TrimEnd('/') + path;
        }

        private readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }

        };
    }
}
