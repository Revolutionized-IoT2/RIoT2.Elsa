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
using RIoT2.Core.Models;
using RIoT2.Elsa.Workflows.Activities;
using RIoT2.Elsa.Workflows.Automation;
using RIoT2.Elsa.Workflows.Llm;
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
        ("price-based-heater.json", PriceBasedHeater()),
        ("forecast-preheat.json", ForecastPreheat()),
        ("llm-heat-schedule.json", LlmHeatSchedule())
    ];

    /// <summary>
    /// When tomorrow's prices arrive: if tomorrow is cold (below 3 °C), today is cheaper than tomorrow and it is at most 20 °C
    /// inside, the heat pump goes to 22 °C in today's remaining cheapest price slots and back to 20 °C after each.
    /// </summary>
    public static Workflow ForecastPreheat()
    {
        var prices = At(new RIoTTrigger { Id = "prices", Name = "PricesPublished", CanStartWorkflow = true, SelectedTrigger = Selection("Choose the electricity prices (forecast) report", TemplateType.Report, null) }, 0, 0);
        var tomorrowTemperature = At(new RIoTForecast { Id = "tomorrowTemperature", Name = "TomorrowTemperature", Forecast = Selection("Choose the outside temperature forecast", TemplateType.Report, null), Window = new Input<string>(ForecastWindows.Tomorrow) }, 300, 0);
        var cold = At(new RIoTCompare { Id = "coldTomorrow", Name = "ColdTomorrow", Value = Script("return getOutputFrom('TomorrowTemperature', 'Min');"), Operator = new Input<string>("less than"), Expected = new Input<string>("3") }, 600, 0);
        var today = At(new RIoTForecast { Id = "pricesToday", Name = "PricesToday", Forecast = Selection("Choose the electricity prices (forecast) report", TemplateType.Report, null), Window = new Input<string>(ForecastWindows.Today) }, 900, 0);
        var tomorrow = At(new RIoTForecast { Id = "pricesTomorrow", Name = "PricesTomorrow", Forecast = Selection("Choose the electricity prices (forecast) report", TemplateType.Report, null), Window = new Input<string>(ForecastWindows.Tomorrow) }, 1200, 0);
        var cheaper = At(new RIoTCompare { Id = "todayCheaper", Name = "TodayCheaper", Value = Script("return getOutputFrom('PricesToday', 'Average') - getOutputFrom('PricesTomorrow', 'Average');"), Operator = new Input<string>("less than"), Expected = new Input<string>("0") }, 1500, 0);
        var inside = At(new RIoTData { Id = "insideTemperature", Name = "InsideTemperature", SelectedDataSource = Selection("Choose the inside temperature report", TemplateType.Report, null) }, 1800, 0);
        var notWarm = At(new RIoTCompare { Id = "insideAtMost20", Name = "InsideAtMost20", Value = Script("return getOutputFrom('InsideTemperature', 'DataObject');"), Operator = new Input<string>("at most"), Expected = new Input<string>("20") }, 2100, 0);
        var cheapest = At(new RIoTForecast
        {
            Id = "cheapestToday", Name = "CheapestSlotsLeftToday", Forecast = Selection("Choose the electricity prices (forecast) report", TemplateType.Report, null),
            Window = new Input<string>(ForecastWindows.Range), From = Script<string?>("return new Date().toISOString();"),
            To = Script<string?>("const end = new Date(); end.setHours(24, 0, 0, 0); return end.toISOString();"),
            Rank = new Input<int>(8), RequireComplete = new Input<bool>(false)
        }, 2400, 0);
        var plan = At(new RIoTCommandPlan
        {
            Id = "preheat", Name = "Preheat", Command = Selection("Choose the heat pump's temperature command", TemplateType.Command, null),
            PlanName = new Input<string?>("Preheat before a cold day"),
            Steps = Script("""
                // 22 °C in each run of cheap slots, 20 °C after it.
                const cheap = Array.from(getOutputFrom('CheapestSlotsLeftToday', 'Lowest') || []).sort((a, b) => a.start < b.start ? -1 : 1);
                const steps = [];
                for (const slot of cheap) {
                  const last = steps[steps.length - 1];
                  if (last && last.at === slot.start) { last.at = slot.end; continue; }
                  steps.push({ at: slot.start, value: 22 }, { at: slot.end, value: 20 });
                }
                return steps;
                """)
        }, 2700, 0);
        return Workflow("riot2-template-forecast-preheat", "Template: preheat before a cold day",
            "When tomorrow's electricity prices arrive: if tomorrow's lowest outside temperature is below 3 °C, today's prices are lower on average than tomorrow's, and it is at most 20 °C inside, the heat pump is set to 22 °C in today's 8 remaining cheapest price slots and back to 20 °C after each. Choose the reports in PricesPublished, TomorrowTemperature, PricesToday, PricesTomorrow, InsideTemperature and CheapestSlotsLeftToday, and the heat pump's command in Preheat, then publish.",
            Flowchart(prices, [prices, tomorrowTemperature, cold, today, tomorrow, cheaper, inside, notWarm, cheapest, plan],
                Connect(prices, "Done", tomorrowTemperature),
                Connect(tomorrowTemperature, RIoTForecast.DoneOutcome, cold),
                Connect(cold, RIoTCompare.TrueOutcome, today),
                Connect(today, RIoTForecast.DoneOutcome, tomorrow),
                Connect(tomorrow, RIoTForecast.DoneOutcome, cheaper),
                Connect(cheaper, RIoTCompare.TrueOutcome, inside),
                Connect(inside, "Done", notWarm),
                Connect(notWarm, RIoTCompare.TrueOutcome, cheapest),
                Connect(cheapest, RIoTForecast.DoneOutcome, plan)));
    }

    /// <summary>
    /// When the next 24 hours' lowest temperature forecast drops below 3 °C, a language model plans the heat pump's
    /// temperatures (18 to 22 °C) from the prices, the weather forecast and the inside temperature.
    /// </summary>
    public static Workflow LlmHeatSchedule()
    {
        var forecast = At(new RIoTTrigger { Id = "temperatureForecast", Name = "TemperatureForecast", CanStartWorkflow = true, SelectedTrigger = Selection("Choose the outside temperature forecast", TemplateType.Report, null) }, 0, 0);
        var next = At(new RIoTForecast { Id = "nextDay", Name = "NextDayTemperature", Forecast = Selection("Choose the outside temperature forecast", TemplateType.Report, null), Window = new Input<string>(ForecastWindows.Next), Hours = new Input<int>(24) }, 300, 0);
        var drops = At(new RIoTThreshold { Id = "dropsBelow3", Name = "DropsBelow3", Value = Script("return getOutputFrom('NextDayTemperature', 'Min');"), Upper = new Input<double>(4d), Lower = new Input<double>(3d) }, 600, 0);
        var prices = At(new RIoTForecast { Id = "prices", Name = "Prices", Forecast = Selection("Choose the electricity prices (forecast) report", TemplateType.Report, null), Window = new Input<string>(ForecastWindows.All), RequireComplete = new Input<bool>(false) }, 900, 0);
        var inside = At(new RIoTData { Id = "insideTemperature", Name = "InsideTemperature", SelectedDataSource = Selection("Choose the inside temperature report", TemplateType.Report, null) }, 1200, 0);
        var llm = At(new RIoTRunLlm
        {
            Id = "planWithLlm", Name = "PlanWithLlm",
            Instructions = new Input<string>("Plan the heat pump's target temperature for the next 24 hours, from now. Keep it between 18 and 22 °C. "
                + "Heat more while electricity is cheap and before the coldest hours, and less while it is expensive, so the house stays comfortable (about 20 °C) for the least cost. "
                + "The input has the electricity prices (c/kWh), the outside temperature forecast (°C) and the inside temperature. "
                + "Answer with the steps: each step's time (ISO 8601, a price slot's start) and the temperature from then on. Use at most 12 steps."),
            InputData = Script("""
                return {
                  now: new Date().toISOString(),
                  prices: getOutputFrom('Prices', 'Points'),
                  outsideTemperature: getOutputFrom('NextDayTemperature', 'Points'),
                  insideTemperature: getOutputFrom('InsideTemperature', 'DataObject')
                };
                """),
            ResponseFormat = new Input<string>(LlmResponseFormats.Json),
            JsonSchema = new Input<string?>("""{"type":"object","properties":{"steps":{"type":"array","minItems":1,"maxItems":12,"items":{"type":"object","properties":{"at":{"type":"string"},"value":{"type":"number","minimum":18,"maximum":22}},"required":["at","value"]}},"reason":{"type":"string"}},"required":["steps"]}"""),
            Tools = new Input<string?>(LlmTools.ReadValues)
        }, 1500, 0);
        var plan = At(new RIoTCommandPlan
        {
            Id = "heatPumpPlan", Name = "HeatPumpPlan", Command = Selection("Choose the heat pump's temperature command", TemplateType.Command, null),
            PlanName = new Input<string?>("Language model heat plan"),
            Steps = Script("return getOutputFrom('PlanWithLlm', 'Json');")
        }, 1800, 0);
        return Workflow("riot2-template-llm-heat-schedule", "Template: language model heat schedule",
            "When the lowest outside temperature of the next 24 hours drops below 3 °C (again above 4 °C resets it), a language model plans the heat pump's temperatures between 18 and 22 °C from the electricity prices, the weather forecast and the inside temperature, and the plan replaces the heat pump's previous one. Choose the reports in TemperatureForecast, NextDayTemperature, Prices and InsideTemperature, the model connection in PlanWithLlm and the heat pump's command in HeatPumpPlan, then publish.",
            Flowchart(forecast, [forecast, next, drops, prices, inside, llm, plan],
                Connect(forecast, "Done", next),
                Connect(next, RIoTForecast.DoneOutcome, drops),
                Connect(drops, Hysteresis.Below, prices),
                Connect(prices, RIoTForecast.DoneOutcome, inside),
                Connect(inside, "Done", llm),
                Connect(llm, RIoTRunLlm.DoneOutcome, plan)));
    }

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

    private static Input<object?> Script(string script) => Script<object?>(script);

    private static Input<T> Script<T>(string script) => new(new Expression("JavaScript", script.Replace("\r\n", "\n", StringComparison.Ordinal)));

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
