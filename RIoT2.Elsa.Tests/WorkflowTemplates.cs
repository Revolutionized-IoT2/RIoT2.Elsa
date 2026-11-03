using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elsa.Expressions.Models;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Management.Mappers;
using Elsa.Workflows.Models;
using RIoT2.Elsa.Workflows.Activities;
using RIoT2.Elsa.Workflows.Automation;
using RIoT2.Elsa.Workflows.Models;

namespace RIoT2.Elsa.Tests;

/// <summary>
/// The workflow templates in <c>templates/</c> (I48), built with Elsa's object model so the files are exactly what Elsa
/// Studio exports. <see cref="WorkflowTemplateTests"/> checks that the files match and import.
/// </summary>
internal static class WorkflowTemplates
{
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly DateTimeOffset Created = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<(string File, Workflow Workflow)> All =>
    [
        ("motion-light.json", MotionLight()),
        ("price-based-heater.json", PriceBasedHeater())
    ];

    /// <summary>Motion turns the light on; five minutes after the last motion it goes off.</summary>
    public static Workflow MotionLight()
    {
        var motion = At(new RIoTTrigger { Id = "motion", Name = "Motion", CanStartWorkflow = true, SelectedTrigger = Selection("Choose the motion report", TemplateType.Report, null) }, 0, 0);
        var detected = At(new RIoTCompare { Id = "detected", Name = "MotionDetected", Expected = new Input<string>("true") }, 300, 0);
        var lightOn = At(new RIoTOutput { Id = "lightOn", Name = "LightOn", Command = Selection("Choose the light's command", TemplateType.Command, "return true;") }, 600, -100);
        var quiet = At(new RIoTDebounce { Id = "quiet", Name = "NoMotionFor5Minutes", WindowSeconds = new Input<int>(300) }, 600, 100);
        var lightOff = At(new RIoTOutput { Id = "lightOff", Name = "LightOff", Command = Selection("Choose the light's command", TemplateType.Command, "return false;") }, 900, 100);
        return Workflow("riot2-template-motion-light", "Template: motion light",
            "Motion turns the light on, and it goes off five minutes after the last motion. Choose the motion report in Motion and the light's command in LightOn and LightOff, then publish.",
            Flowchart(motion, [motion, detected, lightOn, quiet, lightOff],
                Connect(motion, "Done", detected),
                Connect(detected, RIoTCompare.TrueOutcome, lightOn),
                Connect(detected, RIoTCompare.TrueOutcome, quiet),
                Connect(quiet, RIoTDebounce.DoneOutcome, lightOff)));
    }

    /// <summary>A heater runs while electricity is cheap: off above 15, on again below 10 (hysteresis).</summary>
    public static Workflow PriceBasedHeater()
    {
        var price = At(new RIoTTrigger { Id = "price", Name = "ElectricityPrice", CanStartWorkflow = true, SelectedTrigger = Selection("Choose the electricity price report", TemplateType.Report, null) }, 0, 0);
        var threshold = At(new RIoTThreshold { Id = "threshold", Name = "PriceThreshold", Upper = new Input<double>(15d), Lower = new Input<double>(10d) }, 300, 0);
        var off = At(new RIoTOutput { Id = "heaterOff", Name = "HeaterOff", Command = Selection("Choose the heater's command", TemplateType.Command, "return false;") }, 600, -100);
        var on = At(new RIoTOutput { Id = "heaterOn", Name = "HeaterOn", Command = Selection("Choose the heater's command", TemplateType.Command, "return true;") }, 600, 100);
        return Workflow("riot2-template-price-based-heater", "Template: price-based heater",
            "The heater goes off when the price rises above 15 and on again when it falls below 10. Choose the price report in ElectricityPrice and the heater's command in HeaterOff and HeaterOn, adjust the limits in PriceThreshold, then publish.",
            Flowchart(price, [price, threshold, off, on],
                Connect(price, "Done", threshold),
                Connect(threshold, Hysteresis.Above, off),
                Connect(threshold, Hysteresis.Below, on)));
    }

    /// <summary>The template as Elsa exports it (and the designer imports it): the definition model, indented.</summary>
    public static string ToJson(Workflow workflow, WorkflowDefinitionMapper mapper, IApiSerializer serializer)
    {
        var model = mapper.Map(workflow);
        var json = JsonNode.Parse(serializer.Serialize(model))!;
        return json.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static Input<RIoTTemplateItem> Selection(string name, TemplateType type, string? value) =>
        // The RIoT selector stores the chosen template as a literal with its JSON.
        new(Expression.LiteralExpression(JsonSerializer.Serialize(new RIoTTemplateItem { Id = "", Name = name, TemplateType = type, Value = value }, Relaxed)));

    private static T At<T>(T activity, int x, int y) where T : IActivity
    {
        activity.Metadata["designer"] = new Dictionary<string, object>
        {
            ["position"] = new Dictionary<string, object> { ["x"] = x, ["y"] = y },
            ["size"] = new Dictionary<string, object> { ["width"] = 220, ["height"] = 60 }
        };
        return activity;
    }

    private static Connection Connect(IActivity source, string outcome, IActivity target) =>
        new(new Endpoint(source, outcome), new Endpoint(target, "In"));

    private static Flowchart Flowchart(IActivity start, IActivity[] activities, params Connection[] connections)
    {
        var flowchart = new Flowchart { Id = "flowchart", Start = start };
        // Elsa records the C# source of an activity built in code; a template has none.
        flowchart.CustomProperties.Remove("source");
        foreach (var activity in activities)
            flowchart.Activities.Add(activity);
        foreach (var connection in connections)
            flowchart.Connections.Add(connection);
        return flowchart;
    }

    private static Workflow Workflow(string definitionId, string name, string description, Flowchart root) => new()
    {
        Id = definitionId + ":1",
        Identity = new WorkflowIdentity(definitionId, 1, definitionId + ":1"),
        Publication = new WorkflowPublication(IsLatest: true, IsPublished: false),
        WorkflowMetadata = new WorkflowMetadata(name, description, Created),
        Root = root
    };
}
