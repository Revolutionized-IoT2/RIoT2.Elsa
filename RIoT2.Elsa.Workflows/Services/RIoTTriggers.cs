using Elsa.Workflows.Helpers;
using Elsa.Workflows.Runtime;
using RIoT2.Elsa.Workflows.Activities;

namespace RIoT2.Elsa.Workflows.Services
{
    /// <summary>
    /// Starts and resumes the workflows that wait for a RIoT report: the remote workflow host's HTTP and gRPC endpoints
    /// and the orchestrator's in-process delivery all come here, so every transport behaves the same way.
    /// </summary>
    public sealed class RIoTTriggers(IServiceScopeFactory scopes, TriggerDeduplicator deduplicator)
    {
        /// <summary>Sends the stimulus of <see cref="RIoTTrigger"/> (and <see cref="RIoTWaitForState"/>) for the report.</summary>
        public static async Task SendAsync(IStimulusSender stimulusSender, string id, object? data, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stimulusSender);
            var activityTypeName = ActivityTypeNameHelper.GenerateTypeName<RIoTTrigger>();
            var metadata = new StimulusMetadata
            {
                Input = new Dictionary<string, object> { ["EventData"] = data! }
            };

            // Starts the workflows whose RIoT Trigger listens to the report, and resumes those waiting for it.
            await stimulusSender.SendAsync(activityTypeName, id, metadata, cancellationToken);
        }

        /// <summary>
        /// Runs the trigger once per <paramref name="messageId"/> (<see cref="TriggerDeduplicator"/>) and counts it in
        /// <see cref="ElsaMetrics.Triggers"/> with the transport.
        /// </summary>
        /// <returns><c>true</c> when the trigger ran, <c>false</c> for a duplicate.</returns>
        public async Task<bool> TriggerOnceAsync(string? messageId, string? id, object? data, string transport, CancellationToken cancellationToken = default)
        {
            id ??= "";
            var ran = await deduplicator.RunOnceAsync(messageId, async () =>
            {
                await using var scope = scopes.CreateAsyncScope();
                await SendAsync(scope.ServiceProvider.GetRequiredService<IStimulusSender>(), id, data, cancellationToken);
            });
            ElsaMetrics.Trigger(ran ? ElsaMetrics.Received : ElsaMetrics.Duplicate, transport);
            return ran;
        }
    }
}
