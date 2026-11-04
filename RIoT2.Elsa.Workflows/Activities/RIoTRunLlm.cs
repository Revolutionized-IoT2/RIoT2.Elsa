using System.Text.Json;
using System.Text.Json.Nodes;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using RIoT2.Elsa.Workflows.Llm;

namespace RIoT2.Elsa.Workflows.Activities
{
    /// <summary>
    /// Has a language model work on the workflow's data (ADR 0016, ported from the Dls Run LLM step): the instructions and
    /// the input go to a model connection the orchestrator keeps, the model may use the allowed tools (read values, send
    /// the allowed commands, within limits), and the answer comes back as text or as JSON checked against a schema, for the
    /// next steps. Continues on <c>Done</c>, or on <c>Failed</c> with the reason in <c>ErrorCode</c> and <c>Error</c>.
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Has a language model answer, decide or act: text or JSON out, optionally reading values and sending the allowed commands",
        DisplayName = "Run LLM",
        Kind = ActivityKind.Task)]
    [FlowNode(DoneOutcome, FailedOutcome)]
    public class RIoTRunLlm : Activity
    {
        public const string DoneOutcome = "Done";
        public const string FailedOutcome = "Failed";

        /// <summary>The designer lists the orchestrator's model connections for this hint.</summary>
        public const string ConnectionUIHint = "riot-llm-connection";

        /// <summary>The designer edits a JSON schema for this hint.</summary>
        public const string JsonSchemaUIHint = "riot-json-schema";

        /// <summary>The designer offers <see cref="LlmTools"/> for this hint.</summary>
        public const string ToolsUIHint = "riot-llm-tools";

        /// <summary>The designer lists the command templates for this hint.</summary>
        public const string CommandListUIHint = "riot-command-list";

        [Input(Description = "The model connection, by its name in the LLM connections.", UIHint = ConnectionUIHint)]
        public Input<string> Connection { get; set; } = new("");

        [Input(Description = "What the model should do, for example: decide whether the heating should be on and explain why.",
            UIHint = InputUIHints.MultiLine)]
        public Input<string> Instructions { get; set; } = new("");

        [Input(Description = "Data for the model, such as the trigger's event data; sent as JSON.", DefaultSyntax = "JavaScript")]
        public Input<object?> InputData { get; set; } = default!;

        [Input(Description = "Text: the answer as text. Json: the answer as JSON, checked against the schema.",
            UIHint = InputUIHints.DropDown, Options = new[] { LlmResponseFormats.Text, LlmResponseFormats.Json }, DefaultValue = LlmResponseFormats.Text)]
        public Input<string> ResponseFormat { get; set; } = new(LlmResponseFormats.Text);

        [Input(Description = "With Json: the JSON schema of the answer; empty for any JSON.", UIHint = JsonSchemaUIHint)]
        public Input<string?> JsonSchema { get; set; } = default!;

        [Input(Description = "The tools the model may use: ReadValues (list and read reports, commands, variables and forecasts), SendCommands (send the allowed commands), PlanCommands (plan values of the allowed commands at future times). Comma-separated; empty for none.",
            UIHint = ToolsUIHint)]
        public Input<string?> Tools { get; set; } = default!;

        [Input(Description = "With SendCommands or PlanCommands: the command ids the model may send or plan, comma-separated or one per line.", UIHint = CommandListUIHint)]
        public Input<string?> AllowedCommands { get; set; } = default!;

        [Input(Description = "Most commands the model may send or plan.", DefaultValue = LlmRunRequest.DefaultMaxCommands)]
        public Input<int> MaxCommands { get; set; } = new(LlmRunRequest.DefaultMaxCommands);

        [Input(Description = "Most tool calls the model may make.", DefaultValue = LlmRunRequest.DefaultMaxToolCalls)]
        public Input<int> MaxToolCalls { get; set; } = new(LlmRunRequest.DefaultMaxToolCalls);

        [Input(Description = "Seconds the model may work, 1 to 300.", DefaultValue = LlmRunRequest.DefaultTimeoutSeconds)]
        public Input<int> TimeoutSeconds { get; set; } = new(LlmRunRequest.DefaultTimeoutSeconds);

        [Output(Description = "The model's answer as text.")]
        public Output<string?> Text { get; set; } = default!;

        [Output(Description = "With Json: the answer as an object.")]
        public Output<object?> Json { get; set; } = default!;

        [Output(Description = "The commands the model sent.")]
        public Output<int> CommandsSent { get; set; } = default!;

        [Output(Description = "The tool calls the model made.")]
        public Output<int> ToolCalls { get; set; } = default!;

        [Output(Description = "Input and output tokens used.")]
        public Output<int> TotalTokens { get; set; } = default!;

        [Output(Description = "Why the step failed, such as llm_timeout or llm_invalid_json.")]
        public Output<string?> ErrorCode { get; set; } = default!;

        [Output(Description = "The failure's description.")]
        public Output<string?> Error { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            LlmRunResult result;
            var request = Request(context, out var problem);
            if (request == null)
            {
                result = LlmRunResult.Failure(LlmErrorCodes.InvalidRequest, problem!);
            }
            else
            {
                var runner = context.GetService<ILlmRunner>();
                if (runner == null)
                {
                    result = LlmRunResult.Failure(LlmErrorCodes.Unavailable, "This workflow host has no LLM service.");
                }
                else
                {
                    // The orchestrator enforces the timeout too; this one also covers the way there.
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds + 15));
                    try
                    {
                        result = await runner.RunAsync(request, timeout.Token);
                    }
                    catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
                    {
                        result = LlmRunResult.Failure(LlmErrorCodes.Timeout, $"No answer within {request.TimeoutSeconds} s.");
                    }
                    catch (HttpRequestException failure)
                    {
                        result = LlmRunResult.Failure(LlmErrorCodes.Unavailable, failure.Message);
                    }
                }
            }

            Text.Set(context, result.Text);
            Json.Set(context, ToObject(result.Json));
            CommandsSent.Set(context, result.Commands.Count);
            ToolCalls.Set(context, result.ToolCalls);
            TotalTokens.Set(context, result.Usage.TotalTokens);
            ErrorCode.Set(context, result.ErrorCode);
            Error.Set(context, result.Error);
            if (!result.Succeeded)
                context.GetRequiredService<ILogger<RIoTRunLlm>>().LogWarning("Run LLM {Activity} failed: {ErrorCode} {Error}", context.Activity.Id, result.ErrorCode, result.Error);
            await context.CompleteActivityWithOutcomesAsync(result.Succeeded ? DoneOutcome : FailedOutcome);
        }

        private LlmRunRequest? Request(ActivityExecutionContext context, out string? problem)
        {
            problem = null;
            JsonNode? schema = null;
            var schemaText = JsonSchema.GetOrDefault(context);
            if (!string.IsNullOrWhiteSpace(schemaText))
            {
                try
                {
                    schema = JsonNode.Parse(schemaText);
                }
                catch (JsonException x)
                {
                    problem = $"The JSON schema is not JSON: {x.Message}";
                    return null;
                }
            }

            JsonNode? input;
            try
            {
                input = ToNode(InputData.GetOrDefault(context));
            }
            catch (Exception x) when (x is JsonException or NotSupportedException)
            {
                problem = $"The input data can't be sent as JSON: {x.Message}";
                return null;
            }

            var request = new LlmRunRequest
            {
                Connection = (Connection.GetOrDefault(context) ?? "").Trim(),
                Instructions = Instructions.GetOrDefault(context) ?? "",
                Input = input,
                ResponseFormat = (ResponseFormat.GetOrDefault(context) ?? LlmResponseFormats.Text).Trim(),
                JsonSchema = schema,
                Tools = List(Tools.GetOrDefault(context)),
                AllowedCommands = List(AllowedCommands.GetOrDefault(context)),
                MaxCommands = MaxCommands.GetOrDefault(context),
                MaxToolCalls = MaxToolCalls.GetOrDefault(context),
                TimeoutSeconds = TimeoutSeconds.GetOrDefault(context),
                WorkflowDefinitionId = context.WorkflowExecutionContext.Workflow.Identity.DefinitionId,
                WorkflowInstanceId = context.WorkflowExecutionContext.Id,
                ActivityId = context.Activity.Id
            };
            problem = request.Validate();
            return problem == null ? request : null;
        }

        /// <summary>Splits a comma- or line-separated list, without blanks or repeats.</summary>
        public static IReadOnlyList<string> List(string? text) =>
            (text ?? "").Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal).ToList();

        private static JsonNode? ToNode(object? value) => value switch
        {
            null => null,
            JsonNode node => node.DeepClone(),
            JsonElement element => JsonNode.Parse(element.GetRawText()),
            string text => JsonValue.Create(text),
            _ => JsonSerializer.SerializeToNode(value)
        };

        // Later steps (JavaScript among them) read plain objects, lists and values.
        private static object? ToObject(JsonNode? node) => node switch
        {
            null => null,
            JsonObject obj => obj.ToDictionary(x => x.Key, x => ToObject(x.Value)),
            JsonArray array => array.Select(ToObject).ToList(),
            JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean,
            JsonValue value when value.TryGetValue<long>(out var integer) => integer,
            JsonValue value when value.TryGetValue<double>(out var number) => number,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            _ => node.ToJsonString()
        };
    }
}
