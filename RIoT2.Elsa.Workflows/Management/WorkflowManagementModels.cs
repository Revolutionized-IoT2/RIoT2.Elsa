using System.Text.Json.Nodes;

namespace RIoT2.Elsa.Workflows.Management
{
    // The workflow management API's messages (ADR 0016). Both hosts serve them as they are: the remote workflow host at
    // /riot/workflows and the orchestrator at /api/workflows, in process or from the remote host. Statuses are strings and
    // the workflow itself is Elsa's definition model JSON (the format of an Elsa export and of templates/), which the
    // designer edits directly.

    /// <summary>A workflow in the list: its latest version and, when one is, its published version.</summary>
    public sealed record WorkflowSummary(
        string DefinitionId,
        string? Name,
        string? Description,
        int LatestVersion,
        int? PublishedVersion,
        bool IsReadonly,
        DateTimeOffset UpdatedAt);

    /// <summary>One version of a workflow.</summary>
    public sealed record WorkflowVersion(string Id, int Version, bool IsLatest, bool IsPublished, DateTimeOffset CreatedAt);

    /// <summary>A version of a workflow with its definition model.</summary>
    public sealed record WorkflowDetails(
        string DefinitionId,
        string Id,
        string? Name,
        string? Description,
        int Version,
        bool IsLatest,
        bool IsPublished,
        int? PublishedVersion,
        bool IsReadonly,
        DateTimeOffset CreatedAt,
        JsonObject Model);

    /// <summary>Saves (and optionally publishes) a definition model; the definition id in the URL wins over the model's.</summary>
    public sealed record SaveWorkflowRequest(JsonObject Model, bool Publish = false);

    /// <summary>The outcome of a save, an import or a publish: the workflow as stored, or why it wasn't.</summary>
    public sealed record WorkflowSaveResult(bool Succeeded, WorkflowDetails? Workflow, IReadOnlyList<string> Errors);

    /// <summary>A workflow as an Elsa export file.</summary>
    public sealed record WorkflowExport(string FileName, JsonObject Model);

    /// <summary>A run (workflow instance).</summary>
    public sealed record WorkflowRunSummary(
        string Id,
        string DefinitionId,
        int Version,
        string? Name,
        string Status,
        string SubStatus,
        int IncidentCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        DateTimeOffset? FinishedAt);

    /// <summary>A page of runs, newest first.</summary>
    public sealed record WorkflowRunsPage(IReadOnlyList<WorkflowRunSummary> Items, long TotalCount, int Page, int PageSize);

    /// <summary>One execution of an activity in a run.</summary>
    public sealed record WorkflowRunActivity(
        string ActivityId,
        string ActivityType,
        string? ActivityName,
        string Status,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt,
        bool HasBookmarks,
        IReadOnlyList<string> Outcomes,
        string? Error);

    /// <summary>A run with the activities it executed, in order.</summary>
    public sealed record WorkflowRunDetails(WorkflowRunSummary Run, IReadOnlyList<WorkflowRunActivity> Activities, IReadOnlyList<string> Incidents);
}
