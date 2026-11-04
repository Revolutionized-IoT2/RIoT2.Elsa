using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Elsa.Workflows.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Workflows.Services;
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

            using var body = new StringContent(RIoT2.Core.Utils.Json.Serialize(c), System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(
                _configuration.OrchestratorBaseUrl.TrimEnd('/') + "/api/command/execute", body, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task<CommandConfirmation> ExecuteCommandWithConfirmationAsync(string id, object? data, int ttlSeconds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A command identifier is required.", nameof(id));
            var request = new { id, value = new ValueModel(data), ttlSeconds, wait = true };
            using var body = new StringContent(RIoT2.Core.Utils.Json.Serialize(request), System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(BuildUrl("/api/v2/commands"), body, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return RIoT2.Core.Utils.Json.Deserialize<CommandConfirmation>(json)
                ?? throw new InvalidOperationException("The orchestrator answered the command without a delivery status.");
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
                    r = RIoT2.Core.Utils.Json.Deserialize<ElsaReport>(json) ?? r;
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
                    c = RIoT2.Core.Utils.Json.Deserialize<Command>(json) ?? c;
                }
            }
            return c;
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
                    v = RIoT2.Core.Utils.Json.Deserialize<Variable>(json) ?? v;
                }
            }
            return v;
        }

        public async Task<ForecastSummary?> GetForecastAsync(string reportId, ForecastRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
            ArgumentNullException.ThrowIfNull(request);
            var query = new List<string> { "window=" + Uri.EscapeDataString(request.Window), "rank=" + request.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (request.Hours != null)
                query.Add("hours=" + request.Hours.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (request.From != null)
                query.Add("from=" + Uri.EscapeDataString(request.From.Value.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            if (request.To != null)
                query.Add("to=" + Uri.EscapeDataString(request.To.Value.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            using var response = await _httpClient.GetAsync(BuildUrl($"/api/v2/forecasts/{Uri.EscapeDataString(reportId)}?{string.Join("&", query)}"), cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                throw new ArgumentException(json);
            response.EnsureSuccessStatusCode();
            return RIoT2.Core.Utils.Json.Deserialize<ForecastSummary>(json);
        }

        public async Task<CommandPlanReceipt> SubmitCommandPlanAsync(CommandPlanSubmission plan, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);
            var body = new
            {
                commandId = plan.CommandId,
                name = plan.Name,
                source = plan.Source,
                catchUp = plan.CatchUp,
                steps = plan.Steps.Select(x => new { at = x.At, value = new ValueModel(x.Value) }).ToList()
            };
            using var content = new StringContent(RIoT2.Core.Utils.Json.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(BuildUrl("/api/v2/command-plans"), content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
                return CommandPlanReceipt.Failure(string.IsNullOrWhiteSpace(json) ? $"The orchestrator refused the plan ({(int)response.StatusCode})." : json.Trim('"'));
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new CommandPlanReceipt
            {
                Succeeded = true,
                PlanId = root.TryGetProperty("id", out var id) ? id.GetString() : null,
                StepCount = root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array ? steps.GetArrayLength() : 0
            };
        }

        public async Task<string?> CancelCommandPlanAsync(string commandId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
            using var response = await _httpClient.DeleteAsync(BuildUrl($"/api/v2/command-plans?commandId={Uri.EscapeDataString(commandId)}"), cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return null;
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(_configuration.OrchestratorBaseUrl))
                throw new InvalidOperationException("The orchestrator URL has not been configured.");

            return _configuration.OrchestratorBaseUrl.TrimEnd('/') + path;
        }
}
}
