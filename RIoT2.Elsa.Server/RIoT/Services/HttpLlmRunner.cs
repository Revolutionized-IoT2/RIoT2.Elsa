using System.Net.Http.Json;
using System.Text.Json;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Workflows.Llm;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    /// <summary>
    /// Runs Run LLM steps on the orchestrator (<c>POST /api/llm/run</c>, ADR 0016): the connections, their secrets, the
    /// tools and the usage records stay there. Sends <c>X-RIoT2-Key</c> and passes the outbound URL policy like
    /// <see cref="RIoTDataService"/>.
    /// </summary>
    public sealed class HttpLlmRunner(IRIoTConfigurationService configuration, HttpClient httpClient) : ILlmRunner
    {
        public const string RunPath = "/api/llm/run";

        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public async Task<LlmRunResult> RunAsync(LlmRunRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(configuration.OrchestratorBaseUrl))
                return LlmRunResult.Failure(LlmErrorCodes.Unavailable, "The orchestrator URL has not been configured.");

            using var response = await httpClient.PostAsJsonAsync(configuration.OrchestratorBaseUrl.TrimEnd('/') + RunPath, request, Options, cancellationToken);
            // The orchestrator answers a failed step with its result too; anything else is the way there failing.
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                var result = await response.Content.ReadFromJsonAsync<LlmRunResult>(Options, cancellationToken);
                if (result != null)
                    return result;
            }
            return LlmRunResult.Failure(LlmErrorCodes.Unavailable, $"The orchestrator answered HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd());
        }
    }
}
