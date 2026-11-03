using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elsa.Common.Entities;
using Elsa.Common.Models;
using Elsa.Expressions.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Management.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Filters;
using Elsa.Workflows.Runtime.OrderDefinitions;

namespace RIoT2.Elsa.Workflows.Management
{
    /// <summary>
    /// What the workflow designer needs from Elsa (ADR 0016), in any host: the workflows and their versions as Elsa
    /// definition models, saving, publishing, retracting, deleting, importing and exporting them, the runs with the
    /// activities they executed, and the activity catalog. It replaces Elsa's REST API and Studio.
    /// </summary>
    public sealed class WorkflowManagementService(
        IWorkflowDefinitionStore definitions,
        IWorkflowDefinitionPublisher publisher,
        IWorkflowDefinitionImporter importer,
        IWorkflowDefinitionExporter exporter,
        IWorkflowDefinitionManager definitionManager,
        IWorkflowInstanceStore instances,
        IActivityExecutionStore activityExecutions,
        IWorkflowCancellationService cancellation,
        IActivityRegistry activityRegistry,
        IExpressionDescriptorRegistry expressionDescriptors,
        IApiSerializer serializer)
    {
        public const int MaxPageSize = 100;

        /// <summary>Every workflow with its latest and published versions, by name.</summary>
        public async Task<IReadOnlyList<WorkflowSummary>> ListAsync(CancellationToken cancellationToken = default)
        {
            var all = (await definitions.FindSummariesAsync(new WorkflowDefinitionFilter { VersionOptions = VersionOptions.All, IsSystem = false }, cancellationToken)).ToList();
            return all.GroupBy(x => x.DefinitionId, StringComparer.Ordinal)
                .Select(group =>
                {
                    var latest = group.FirstOrDefault(x => x.IsLatest) ?? group.MaxBy(x => x.Version ?? 0)!;
                    var published = group.FirstOrDefault(x => x.IsPublished);
                    return new WorkflowSummary(group.Key, latest.Name, latest.Description, latest.Version ?? 0, published?.Version,
                        latest.IsReadonly, latest.CreatedAt);
                })
                .OrderBy(x => x.Name ?? x.DefinitionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>The versions of a workflow, newest first; empty when there is no such workflow.</summary>
        public async Task<IReadOnlyList<WorkflowVersion>> ListVersionsAsync(string definitionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            var versions = await definitions.FindSummariesAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.All }, cancellationToken);
            return versions.OrderByDescending(x => x.Version)
                .Select(x => new WorkflowVersion(x.Id, x.Version ?? 0, x.IsLatest, x.IsPublished, x.CreatedAt))
                .ToList();
        }

        /// <summary>A version of a workflow (the latest when <paramref name="version"/> is null), or null.</summary>
        public async Task<WorkflowDetails?> GetAsync(string definitionId, int? version = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            var options = version is { } number ? VersionOptions.SpecificVersion(number) : VersionOptions.Latest;
            var definition = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = options }, cancellationToken);
            return definition == null ? null : await DetailsAsync(definition, cancellationToken);
        }

        /// <summary>
        /// Saves the model as the workflow's draft: a new version when the latest is published. With
        /// <paramref name="publish"/> the draft is published too. Without a definition id a new workflow is created.
        /// </summary>
        public async Task<WorkflowSaveResult> SaveAsync(string? definitionId, JsonObject model, bool publish, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            WorkflowDefinitionModel definitionModel;
            try
            {
                definitionModel = serializer.Deserialize<WorkflowDefinitionModel>(model.ToJsonString());
            }
            catch (Exception x) when (x is JsonException or NotSupportedException or InvalidOperationException)
            {
                return Failed($"The workflow is not a valid Elsa definition model: {x.Message}");
            }

            if (definitionModel.Root == null)
                return Failed("The workflow has no root activity.");
            if (!string.IsNullOrWhiteSpace(definitionId))
                definitionModel.DefinitionId = definitionId;
            else if (string.IsNullOrWhiteSpace(definitionModel.DefinitionId))
                definitionModel.DefinitionId = null!;

            if (!string.IsNullOrWhiteSpace(definitionModel.DefinitionId))
            {
                var existing = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionModel.DefinitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
                if (existing?.IsReadonly == true)
                    return Failed("The workflow is read-only.");
            }

            var result = await importer.ImportAsync(new SaveWorkflowDefinitionRequest { Model = definitionModel, Publish = publish }, cancellationToken);
            var errors = result.ValidationErrors.Select(x => x.Message).ToList();
            var details = await DetailsAsync(result.WorkflowDefinition, cancellationToken);
            return new WorkflowSaveResult(result.Succeeded, details, errors);
        }

        /// <summary>Imports an Elsa export (or a template) as a new workflow or a new version of its definition id.</summary>
        public Task<WorkflowSaveResult> ImportAsync(JsonObject model, bool publish, CancellationToken cancellationToken = default) =>
            SaveAsync(null, model, publish, cancellationToken);

        /// <summary>Publishes the latest version; null when there is no such workflow.</summary>
        public async Task<WorkflowSaveResult?> PublishAsync(string definitionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            var latest = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
            if (latest == null)
                return null;
            if (latest.IsReadonly)
                return Failed("The workflow is read-only.");
            if (latest.IsPublished)
                return new WorkflowSaveResult(true, await DetailsAsync(latest, cancellationToken), []);

            var result = await publisher.PublishAsync(latest, cancellationToken);
            return new WorkflowSaveResult(result.Succeeded, await DetailsAsync(latest, cancellationToken),
                result.ValidationErrors.Select(x => x.Message).ToList());
        }

        /// <summary>Unpublishes the published version, so its triggers stop; false when none is published.</summary>
        public async Task<bool> RetractAsync(string definitionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            var published = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Published }, cancellationToken);
            if (published == null || published.IsReadonly)
                return false;
            await publisher.RetractAsync(published, cancellationToken);
            return true;
        }

        /// <summary>Deletes every version of the workflow; false when there is none.</summary>
        public async Task<bool> DeleteAsync(string definitionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            return await definitionManager.DeleteByDefinitionIdAsync(definitionId, cancellationToken) > 0;
        }

        /// <summary>The workflow's latest version as an Elsa export, or null.</summary>
        public async Task<WorkflowExport?> ExportAsync(string definitionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            var export = await exporter.ExportAsync(definitionId, VersionOptions.Latest, cancellationToken: cancellationToken);
            return export == null ? null : new WorkflowExport(export.FileName, ParseObject(export.Data));
        }

        /// <summary>Runs, newest first, of one workflow or all; optionally only those with an Elsa status (Running, Finished).</summary>
        public async Task<WorkflowRunsPage> ListRunsAsync(string? definitionId, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 0);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var filter = new WorkflowInstanceFilter { DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? null : definitionId, IsSystem = false };
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<WorkflowStatus>(status, true, out var workflowStatus))
                    filter.WorkflowStatus = workflowStatus;
                else if (Enum.TryParse<WorkflowSubStatus>(status, true, out var subStatus))
                    filter.WorkflowSubStatus = subStatus;
                else
                    throw new ArgumentException($"Unknown run status '{status}'.", nameof(status));
            }

            var order = new WorkflowInstanceOrder<DateTimeOffset>(x => x.CreatedAt, OrderDirection.Descending);
            var result = await instances.SummarizeManyAsync(filter, PageArgs.FromPage(page, pageSize), order, cancellationToken);
            return new WorkflowRunsPage(result.Items.Select(Map).ToList(), result.TotalCount, page, pageSize);
        }

        /// <summary>A run with its activity executions in the order they started, or null.</summary>
        public async Task<WorkflowRunDetails?> GetRunAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            var instance = await instances.FindAsync(new WorkflowInstanceFilter { Id = instanceId }, cancellationToken);
            if (instance == null)
                return null;

            var records = await activityExecutions.FindManyAsync(new ActivityExecutionRecordFilter { WorkflowInstanceId = instanceId },
                new ActivityExecutionRecordOrder<DateTimeOffset>(x => x.StartedAt, OrderDirection.Ascending), cancellationToken);
            var activities = records.Select(x => new WorkflowRunActivity(x.ActivityId, x.ActivityType, x.ActivityName, x.Status.ToString(),
                x.StartedAt, x.CompletedAt, x.HasBookmarks, Outcomes(x.Payload), x.Exception?.Message)).ToList();
            var incidents = instance.WorkflowState.Incidents.Select(x => $"{x.ActivityId}: {x.Message}").ToList();
            return new WorkflowRunDetails(Map(WorkflowInstanceSummary.FromInstance(instance)), activities, incidents);
        }

        /// <summary>Cancels a run that has not finished; false when there is no such run or it has finished.</summary>
        public Task<bool> CancelRunAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            return cancellation.CancelWorkflowAsync(instanceId, cancellationToken);
        }

        /// <summary>The activity catalog the designer shows and writes (<see cref="WorkflowCatalogBuilder"/>).</summary>
        public JsonObject GetCatalog() => WorkflowCatalogBuilder.Build(activityRegistry, serializer, expressionDescriptors);

        private async Task<WorkflowDetails> DetailsAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
        {
            var export = await exporter.ExportDefinitionAsync(definition, cancellationToken);
            var published = definition.IsPublished
                ? definition
                : await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definition.DefinitionId, VersionOptions = VersionOptions.Published }, cancellationToken);
            return new WorkflowDetails(definition.DefinitionId, definition.Id, definition.Name, definition.Description, definition.Version,
                definition.IsLatest, definition.IsPublished, published?.Version, definition.IsReadonly, definition.CreatedAt, ParseObject(export.Data));
        }

        private static WorkflowRunSummary Map(WorkflowInstanceSummary x) => new(x.Id, x.DefinitionId, x.Version, x.Name,
            x.Status.ToString(), x.SubStatus.ToString(), x.IncidentCount, x.CreatedAt, x.UpdatedAt, x.FinishedAt);

        private static WorkflowSaveResult Failed(string error) => new(false, null, [error]);

        private static JsonObject ParseObject(byte[] data) =>
            JsonNode.Parse(Encoding.UTF8.GetString(data)) as JsonObject ?? throw new InvalidOperationException("Elsa exported something other than a JSON object.");

        // Elsa keeps the outcomes an activity completed with in its execution record's payload.
        private static IReadOnlyList<string> Outcomes(IDictionary<string, object>? payload)
        {
            if (payload == null || !payload.TryGetValue("Outcomes", out var value) && !payload.TryGetValue("outcomes", out value))
                return [];
            return value switch
            {
                string single => [single],
                JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Select(x => x.ToString()).ToList(),
                IEnumerable items => items.Cast<object?>().Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList(),
                _ => []
            };
        }
    }
}
