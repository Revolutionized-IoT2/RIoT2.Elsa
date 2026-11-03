using Elsa.Common.Models;
using Elsa.Common.Serialization;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Mappers;
using Elsa.Workflows.Management.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Elsa.Workflows.Activities;
using RIoT2.Elsa.Workflows;

namespace RIoT2.Elsa.Tests;

/// <summary>The workflow templates in <c>templates/</c> (I48): up to date with <see cref="WorkflowTemplates"/>, and importable.</summary>
[TestClass]
public class WorkflowTemplateTests
{
    /// <summary>Set to <c>1</c> to write the template files instead of comparing them.</summary>
    public const string UpdateVariable = "RIOT2_UPDATE_TEMPLATES";

    [TestMethod]
    public async Task TheTemplateFilesAreUpToDate()
    {
        await using var provider = await ServicesAsync();
        var mapper = provider.GetRequiredService<WorkflowDefinitionMapper>();
        var serializer = provider.GetRequiredService<IApiSerializer>();
        foreach (var (file, workflow) in WorkflowTemplates.All)
        {
            var path = Path.Combine(TemplateFolder(), file);
            var json = WorkflowTemplates.ToJson(workflow, mapper, serializer);
            if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
                await File.WriteAllTextAsync(path, json);
            Assert.IsTrue(File.Exists(path), $"{file} is missing; run the tests with {UpdateVariable}=1");
            Assert.AreEqual(json, (await File.ReadAllTextAsync(path)).Replace("\r\n", "\n", StringComparison.Ordinal),
                $"{file} differs from WorkflowTemplates; run the tests with {UpdateVariable}=1");
        }
    }

    [TestMethod]
    [DataRow("motion-light.json", "riot2-template-motion-light", new[] { "RIoTTrigger", "RIoTCompare", "RIoTOutput", "RIoTDebounce", "RIoTOutput" })]
    [DataRow("price-based-heater.json", "riot2-template-price-based-heater", new[] { "RIoTTrigger", "RIoTThreshold", "RIoTOutput", "RIoTOutput" })]
    public async Task TheTemplatesImportAndPublishLikeAStudioImport(string file, string definitionId, string[] activities)
    {
        await using var provider = await ServicesAsync();
        var serializer = provider.GetRequiredService<IApiSerializer>();
        var model = serializer.Deserialize<WorkflowDefinitionModel>(await File.ReadAllTextAsync(Path.Combine(TemplateFolder(), file)));

        // The service behind Elsa Studio's import (POST /elsa/api/workflow-definitions/import).
        var result = await provider.GetRequiredService<IWorkflowDefinitionImporter>().ImportAsync(new SaveWorkflowDefinitionRequest { Model = model, Publish = true });

        Assert.IsTrue(result.Succeeded, string.Join("; ", result.ValidationErrors.Select(e => e.Message)));
        var graph = await provider.GetRequiredService<IWorkflowDefinitionService>().FindWorkflowGraphAsync(definitionId, VersionOptions.Published);
        var flowchart = (Flowchart)graph!.Workflow.Root;
        CollectionAssert.AreEqual(activities, flowchart.Activities.Select(a => a.GetType().Name).ToArray());
        Assert.IsFalse(flowchart.Connections.Any(c => c.Target.Activity is RIoTTrigger), "the trigger starts the flowchart");
        Assert.IsNotEmpty(flowchart.Connections);
        foreach (var output in flowchart.Activities.OfType<RIoTOutput>())
            Assert.IsNotNull(output.Command.Expression?.Value, "the selector's literal survives the import");
    }

    [TestMethod]
    public async Task TemplatesWithTheTypeNameOfElsa37StillImport()
    {
        await using var provider = await ServicesAsync();
        var serializer = provider.GetRequiredService<IApiSerializer>();
        var json = (await File.ReadAllTextAsync(Path.Combine(TemplateFolder(), "motion-light.json")))
            .Replace($"\"{RIoTFeature.TemplateItemAlias}\"", $"\"{RIoTFeature.LegacyTemplateItemTypeName}\"", StringComparison.Ordinal);
        Assert.Contains(RIoTFeature.LegacyTemplateItemTypeName, json);
        var model = serializer.Deserialize<WorkflowDefinitionModel>(json);

        var result = await provider.GetRequiredService<IWorkflowDefinitionImporter>().ImportAsync(new SaveWorkflowDefinitionRequest { Model = model, Publish = true });

        Assert.IsTrue(result.Succeeded, string.Join("; ", result.ValidationErrors.Select(e => e.Message)));
        var graph = await provider.GetRequiredService<IWorkflowDefinitionService>().FindWorkflowGraphAsync("riot2-template-motion-light", VersionOptions.Published);
        foreach (var output in ((Flowchart)graph!.Workflow.Root).Activities.OfType<RIoTOutput>())
            Assert.IsNotNull(output.Command.Expression?.Value, "the selector's literal survives the import");
    }

    private static async Task<ServiceProvider> ServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<SerializationTypeOptions>(RIoTFeature.RegisterTypes);
        services.AddElsa(elsa => elsa
            .UseWorkflowManagement()
            .UseWorkflowRuntime()
            .UseJavaScript()
            .UseScheduling()
            .AddActivitiesFrom<RIoTCompare>());
        var provider = services.BuildServiceProvider();
        // Elsa's host fills the activity registry at startup; the activity JSON needs it.
        await provider.GetRequiredService<IActivityRegistryPopulator>().PopulateRegistryAsync();
        return provider;
    }

    private static string TemplateFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RIoT2.Elsa.sln")))
                return Path.Combine(directory.FullName, "templates");
        }
        throw new InvalidOperationException("RIoT2.Elsa.sln not found above " + AppContext.BaseDirectory);
    }
}
