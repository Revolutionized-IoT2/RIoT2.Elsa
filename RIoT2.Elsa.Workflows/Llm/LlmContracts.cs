using System.Text.Json.Nodes;

namespace RIoT2.Elsa.Workflows.Llm
{
    // The Run LLM activity's port (ADR 0016): the orchestrator owns the model connections and their secrets, talks to the
    // model, runs the tools and records the usage. In the embedded mode the activity calls it in process; in the remote
    // mode the workflow host posts the request to the orchestrator's POST /api/llm/run.

    /// <summary>The tools a Run LLM step may give the model.</summary>
    public static class LlmTools
    {
        /// <summary>Lists the reports, commands and variables, and reads their current values.</summary>
        public const string ReadValues = "ReadValues";

        /// <summary>Sends the allowed commands, at most <see cref="LlmRunRequest.MaxCommands"/> of them.</summary>
        public const string SendCommands = "SendCommands";

        public static readonly IReadOnlyList<string> All = [ReadValues, SendCommands];
    }

    /// <summary>The answer formats of a Run LLM step.</summary>
    public static class LlmResponseFormats
    {
        public const string Text = "Text";
        public const string Json = "Json";
    }

    /// <summary>Why a Run LLM step failed (<see cref="LlmRunResult.ErrorCode"/>).</summary>
    public static class LlmErrorCodes
    {
        public const string UnknownConnection = "llm_unknown_connection";
        public const string ConnectionDisabled = "llm_connection_disabled";
        public const string InvalidRequest = "llm_invalid_request";
        public const string ProviderError = "llm_provider_error";
        public const string Timeout = "llm_timeout";
        public const string ToolCallLimit = "llm_tool_call_limit";
        public const string InvalidJson = "llm_invalid_json";
        public const string Unavailable = "llm_unavailable";
    }

    /// <summary>What a Run LLM step asks of the model.</summary>
    public sealed record LlmRunRequest
    {
        public const int DefaultMaxToolCalls = 10;
        public const int MaxMaxToolCalls = 50;
        public const int DefaultMaxCommands = 3;
        public const int MaxMaxCommands = 20;
        public const int DefaultTimeoutSeconds = 60;
        public const int MaxTimeoutSeconds = 300;

        /// <summary>The model connection, by name.</summary>
        public required string Connection { get; init; }

        /// <summary>What the model should do; workflow expressions are already evaluated.</summary>
        public required string Instructions { get; init; }

        /// <summary>Data for the model, such as the trigger's event data; sent as JSON.</summary>
        public JsonNode? Input { get; init; }

        /// <summary><see cref="LlmResponseFormats.Text"/> or <see cref="LlmResponseFormats.Json"/>.</summary>
        public string ResponseFormat { get; init; } = LlmResponseFormats.Text;

        /// <summary>With <see cref="LlmResponseFormats.Json"/>: the JSON schema the answer must match; empty for any JSON.</summary>
        public JsonNode? JsonSchema { get; init; }

        /// <summary>The <see cref="LlmTools"/> the model may use.</summary>
        public IReadOnlyList<string> Tools { get; init; } = [];

        /// <summary>With <see cref="LlmTools.SendCommands"/>: the command ids the model may send.</summary>
        public IReadOnlyList<string> AllowedCommands { get; init; } = [];

        public int MaxCommands { get; init; } = DefaultMaxCommands;
        public int MaxToolCalls { get; init; } = DefaultMaxToolCalls;
        public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

        /// <summary>Where the step runs, for the usage records.</summary>
        public string? WorkflowDefinitionId { get; init; }
        public string? WorkflowInstanceId { get; init; }
        public string? ActivityId { get; init; }

        /// <summary>The first problem with the request, or null when it is valid.</summary>
        public string? Validate()
        {
            if (string.IsNullOrWhiteSpace(Connection))
                return "A model connection is required.";
            if (string.IsNullOrWhiteSpace(Instructions))
                return "Instructions are required.";
            if (ResponseFormat is not (LlmResponseFormats.Text or LlmResponseFormats.Json))
                return $"The response format must be {LlmResponseFormats.Text} or {LlmResponseFormats.Json}.";
            if (JsonSchema != null && JsonSchema is not JsonObject)
                return "The JSON schema must be a JSON object.";
            if (Tools.FirstOrDefault(x => !LlmTools.All.Contains(x)) is { } tool)
                return $"Unknown tool '{tool}'; the tools are {string.Join(", ", LlmTools.All)}.";
            if (MaxToolCalls is < 0 or > MaxMaxToolCalls)
                return $"The tool call limit must be 0-{MaxMaxToolCalls}.";
            if (MaxCommands is < 0 or > MaxMaxCommands)
                return $"The command limit must be 0-{MaxMaxCommands}.";
            if (TimeoutSeconds is < 1 or > MaxTimeoutSeconds)
                return $"The timeout must be 1-{MaxTimeoutSeconds} seconds.";
            return null;
        }
    }

    /// <summary>A command the model sent, and how its delivery went.</summary>
    public sealed record LlmSentCommand(string Id, JsonNode? Value, string Status);

    /// <summary>The tokens a step used.</summary>
    public sealed record LlmUsage(int InputTokens, int OutputTokens)
    {
        public int TotalTokens => InputTokens + OutputTokens;
    }

    /// <summary>What a Run LLM step got.</summary>
    public sealed record LlmRunResult
    {
        public bool Succeeded { get; init; }

        /// <summary>The model's final answer.</summary>
        public string? Text { get; init; }

        /// <summary>With <see cref="LlmResponseFormats.Json"/>: the answer parsed (and checked against the schema).</summary>
        public JsonNode? Json { get; init; }

        public IReadOnlyList<LlmSentCommand> Commands { get; init; } = [];
        public int ToolCalls { get; init; }
        public LlmUsage Usage { get; init; } = new(0, 0);

        /// <summary>One of <see cref="LlmErrorCodes"/> when the step failed.</summary>
        public string? ErrorCode { get; init; }
        public string? Error { get; init; }

        public static LlmRunResult Failure(string errorCode, string error, LlmUsage? usage = null, int toolCalls = 0,
            IReadOnlyList<LlmSentCommand>? commands = null) =>
            new() { Succeeded = false, ErrorCode = errorCode, Error = error, Usage = usage ?? new(0, 0), ToolCalls = toolCalls, Commands = commands ?? [] };
    }

    /// <summary>Runs a Run LLM step: the orchestrator's LLM service, in process or over HTTP.</summary>
    public interface ILlmRunner
    {
        Task<LlmRunResult> RunAsync(LlmRunRequest request, CancellationToken cancellationToken = default);
    }
}
