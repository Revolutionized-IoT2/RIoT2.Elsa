using System.Net.Http;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Elsa.Server.RIoT.Activities;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Studio.Models;
using Template = RIoT2.Elsa.Server.RIoT.Models.Template;

namespace RIoT2.Elsa.Tests;

[TestClass]
public class RIoTDataActivityTests
{
    [TestMethod]
    public async Task DataActivityAwaitsDataLookupAndRecordsFailure()
    {
        var data = new BlockingDataService();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddElsa(elsa => elsa.AddActivitiesFrom<RIoTData>());
        services.AddSingleton<IRIoTDataService>(data);
        await using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<IWorkflowRunner>();
        var activity = new RIoTData
        {
            SelectedDataSource = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "temp", TemplateType = TemplateType.Report })
        };

        var execution = runner.RunAsync(activity);
        await data.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsFalse(execution.IsCompleted, "Elsa must await the data lookup instead of blocking on .Result.");

        data.Release.TrySetResult();
        var result = await execution.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(1, result.WorkflowState.Incidents.Count);
        StringAssert.Contains(result.WorkflowState.Incidents.Single().Message, "Injected data failure");
    }

    private sealed class BlockingDataService : IRIoTDataService
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<object> GetReportValueAsync(string reportId)
        {
            Assert.AreEqual("temp", reportId);
            Entered.TrySetResult();
            await Release.Task;
            throw new HttpRequestException("Injected data failure");
        }

        public Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Template>> GetReportTemplatesAsync() => throw new NotSupportedException();
        public Task<List<Template>> GetCommandTemplatesAsync() => throw new NotSupportedException();
        public Task<List<Template>> GetVariableTemplatesAsync() => throw new NotSupportedException();
        public Task<object> GetCommandValueAsync(string commandId) => throw new NotSupportedException();
        public Task<object> GetVariableValueAsync(string variableId) => throw new NotSupportedException();
    }
}
