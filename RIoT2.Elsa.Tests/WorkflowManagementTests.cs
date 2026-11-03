using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RIoT2.Elsa.Tests;

/// <summary>
/// The workflow management service (ADR 0016) on a running engine, as both hosts compose it with
/// <see cref="WorkflowsServiceCollectionExtensions.AddRIoT2Workflows"/>: what the designer reads and writes.
/// </summary>
[TestClass]
[DoNotParallelize]
public class WorkflowManagementTests
{
    private static string _folder = "";
    private static IHost _host = null!;
    private static readonly RecordingDataService Data = new();

    [ClassInitialize]
    public static async Task StartAsync(TestContext _)
    {
        _folder = Path.Combine(Path.GetTempPath(), "riot2-elsa-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddRIoT2Workflows(new RIoT2WorkflowsOptions
        {
            ConnectionString = $"Data Source={Path.Combine(_folder, "elsa.sqlite.db")};Cache=Shared;",
            HttpActivities = false
        });
        builder.Services.AddSingleton<IRIoTDataService>(Data);
        _host = builder.Build();
        await _host.StartAsync();
    }

    [ClassCleanup]
    public static async Task StopAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, true); } catch (IOException) { }
    }

    [TestMethod]
    public async Task ATemplateImportsAndIsEditedPublishedExportedRetractedAndDeleted()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkflowManagementService>();
        var template = await TemplateAsync("price-based-heater.json");

        var imported = await service.ImportAsync(template, publish: false);
        Assert.IsTrue(imported.Succeeded, string.Join("; ", imported.Errors));
        var id = imported.Workflow!.DefinitionId;
        Assert.AreEqual("riot2-template-price-based-heater", id);
        Assert.IsNull(imported.Workflow.PublishedVersion);
        Assert.AreEqual("Template: price-based heater", (await service.ListAsync()).Single(x => x.DefinitionId == id).Name);

        // The designer edits the model it got and saves it back.
        var model = (await service.GetAsync(id))!.Model;
        model["name"] = "Heater";
        var saved = await service.SaveAsync(id, model, publish: true);
        Assert.IsTrue(saved.Succeeded, string.Join("; ", saved.Errors));
        Assert.AreEqual("Heater", saved.Workflow!.Name);
        Assert.IsTrue(saved.Workflow.IsPublished);
        Assert.AreEqual(saved.Workflow.Version, saved.Workflow.PublishedVersion);
        var root = saved.Workflow.Model["root"]!.AsObject();
        Assert.AreEqual("Elsa.Flowchart", root["type"]!.GetValue<string>());
        Assert.AreEqual(RIoTFeature.TemplateItemAlias, root["activities"]![0]!["selectedTrigger"]!["typeName"]!.GetValue<string>());

        // A change to a published workflow is a new draft version; the published one keeps running.
        model = saved.Workflow.Model;
        model["description"] = "Changed";
        var draft = await service.SaveAsync(id, model, publish: false);
        Assert.IsTrue(draft.Succeeded, string.Join("; ", draft.Errors));
        Assert.AreEqual(saved.Workflow.Version + 1, draft.Workflow!.Version);
        Assert.AreEqual(saved.Workflow.Version, draft.Workflow.PublishedVersion);
        var versions = await service.ListVersionsAsync(id);
        Assert.AreEqual(draft.Workflow.Version, versions[0].Version);
        Assert.IsTrue(versions.Single(x => x.Version == saved.Workflow.Version).IsPublished);

        var export = await service.ExportAsync(id);
        Assert.IsNotNull(export);
        Assert.AreEqual("Changed", export.Model["description"]!.GetValue<string>());

        Assert.IsTrue(await service.RetractAsync(id));
        Assert.IsNull((await service.GetAsync(id))!.PublishedVersion);
        Assert.IsTrue(await service.DeleteAsync(id));
        Assert.IsNull(await service.GetAsync(id));
        Assert.IsFalse(await service.DeleteAsync(id));
    }

    [TestMethod]
    public async Task AnInvalidModelIsRefusedWithAReason()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkflowManagementService>();

        var result = await service.SaveAsync("broken", new JsonObject { ["name"] = "No root" }, publish: false);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Workflow);
        Assert.IsNotEmpty(result.Errors);
        Assert.IsNull(await service.GetAsync("broken"));
    }

    [TestMethod]
    public async Task TheCatalogDescribesTheRIoTActivitiesForTheDesigner()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<WorkflowManagementService>().GetCatalog();

        var activities = catalog["activities"]!.AsArray().Select(x => x!.AsObject()).ToList();
        var trigger = activities.Single(x => x["type"]!.GetValue<string>() == "RIoT2.RIoTTrigger");
        Assert.AreEqual("Trigger", trigger["kind"]!.GetValue<string>());
        var selector = trigger["inputs"]!.AsArray().Single(x => x!["key"]!.GetValue<string>() == "selectedTrigger")!;
        Assert.AreEqual("riot-output-selector", selector["uiHint"]!.GetValue<string>());
        var settings = selector["options"]!["riot-output-selector"]!;
        CollectionAssert.AreEqual(new[] { "Report", "Variable" }, settings["templateTypes"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.IsTrue(settings["hideEditor"]!.GetValue<bool>());

        var output = activities.Single(x => x["type"]!.GetValue<string>() == "RIoT2.RIoTOutput");
        Assert.IsFalse(output["inputs"]!.AsArray().Single()!["options"]!["riot-output-selector"]!["hideEditor"]!.GetValue<bool>());
        var compare = activities.Single(x => x["type"]!.GetValue<string>() == "RIoT2.RIoTCompare");
        CollectionAssert.AreEquivalent(new[] { "True", "False" }, compare["outcomes"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        var llm = activities.Single(x => x["type"]!.GetValue<string>() == "RIoT2.RIoTRunLlm");
        Assert.AreEqual("Run LLM", llm["displayName"]!.GetValue<string>());
        CollectionAssert.AreEquivalent(new[] { "Done", "Failed" }, llm["outcomes"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        var hints = llm["inputs"]!.AsArray().ToDictionary(x => x!["key"]!.GetValue<string>(), x => x!["uiHint"]?.GetValue<string>());
        Assert.AreEqual(RIoTRunLlm.ConnectionUIHint, hints["connection"]);
        Assert.AreEqual(RIoTRunLlm.ToolsUIHint, hints["tools"]);
        Assert.AreEqual("dropdown", hints["responseFormat"]);
        Assert.IsTrue(activities.Any(x => x["type"]!.GetValue<string>() == "Elsa.RunJavaScript"));
        Assert.Contains("JavaScript", catalog["languages"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        // Triggers come first, the RIoT ones before Elsa's.
        Assert.AreEqual("Trigger", activities[0]["kind"]!.GetValue<string>());
        StringAssert.StartsWith(activities[0]["type"]!.GetValue<string>(), "RIoT2.");
    }

    [TestMethod]
    public async Task AReportRunsThePublishedWorkflowAndTheRunShowsItsActivities()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkflowManagementService>();
        // The motion light, with its selections made: the motion report and the light's command.
        var json = (await File.ReadAllTextAsync(Path.Combine(TemplateFolder(), "motion-light.json")))
            .Replace("\\\"Id\\\":\\\"\\\",\\\"Name\\\":\\\"Choose the motion report\\\"", "\\\"Id\\\":\\\"motion-report\\\",\\\"Name\\\":\\\"Choose the motion report\\\"", StringComparison.Ordinal)
            .Replace("\\\"Id\\\":\\\"\\\",\\\"Name\\\":\\\"Choose the light's command\\\"", "\\\"Id\\\":\\\"light\\\",\\\"Name\\\":\\\"Choose the light's command\\\"", StringComparison.Ordinal);
        var model = JsonNode.Parse(json)!.AsObject();
        model["definitionId"] = "motion-light-run";
        var saved = await service.SaveAsync("motion-light-run", model, publish: true);
        Assert.IsTrue(saved.Succeeded, string.Join("; ", saved.Errors));

        Assert.IsTrue(await _host.Services.GetRequiredService<RIoTTriggers>().TriggerOnceAsync("m-1", "motion-report", true, "test"));

        // Elsa may start the run in the background.
        var runs = await EventuallyAsync(() => service.ListRunsAsync("motion-light-run", null, 0, 10), x => x.TotalCount > 0);
        Assert.AreEqual(1, runs.TotalCount);
        var details = await EventuallyAsync(() => service.GetRunAsync(runs.Items[0].Id), x => x!.Activities.Any(a => a.ActivityId == "lightOn"));
        Assert.IsNotNull(details);
        var executed = details.Activities.Select(x => x.ActivityId).ToList();
        CollectionAssert.IsSubsetOf(new[] { "motion", "detected", "lightOn" }, executed);
        Assert.AreEqual("light", Data.Commands.Single().Id);
        Assert.AreEqual(true, Data.Commands.Single().Value);
        // The debounce waits; the run can be cancelled.
        Assert.AreEqual("Running", details.Run.Status);
        Assert.IsTrue(await service.CancelRunAsync(details.Run.Id));
        Assert.AreEqual("Cancelled", (await EventuallyAsync(() => service.GetRunAsync(details.Run.Id), x => x!.Run.SubStatus == "Cancelled"))!.Run.SubStatus);
        Assert.AreEqual(0, (await service.ListRunsAsync("motion-light-run", "Running", 0, 10)).TotalCount);
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var value = await read();
            if (done(value) || DateTime.UtcNow > until)
                return value;
            await Task.Delay(100);
        }
    }

    private static async Task<JsonObject> TemplateAsync(string file) =>
        JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(TemplateFolder(), file)))!.AsObject();

    private static string TemplateFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RIoT2.Elsa.sln")))
                return Path.Combine(directory.FullName, "templates");
        }
        throw new InvalidOperationException("RIoT2.Elsa.sln not found above " + AppContext.BaseDirectory);
    }

    private sealed class RecordingDataService : IRIoTDataService
    {
        public List<(string Id, object? Value)> Commands { get; } = [];

        public Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default)
        {
            lock (Commands)
                Commands.Add((id, data));
            return Task.CompletedTask;
        }
        public Task<object> GetReportValueAsync(string reportId) => Task.FromResult<object>(new ElsaReport { Id = reportId });
        public Task<object> GetCommandValueAsync(string commandId) => throw new NotSupportedException();
        public Task<object> GetVariableValueAsync(string variableId) => throw new NotSupportedException();
        public Task<CommandConfirmation> ExecuteCommandWithConfirmationAsync(string id, object? data, int ttlSeconds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
