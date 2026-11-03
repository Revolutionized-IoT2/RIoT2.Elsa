using System.Text.Json.Nodes;
using Elsa.Expressions.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Models;

namespace RIoT2.Elsa.Workflows.Management
{
    /// <summary>
    /// The designer's activity catalog (ported from Dls): for every browsable activity, what the engine says about it,
    /// reduced to what the designer shows and writes: its inputs (with the camelCase keys the definition model uses, their
    /// UI hint and options, such as <c>riot-output-selector</c>'s template kinds), outputs, outcomes and embedded slots;
    /// and the expression languages.
    /// </summary>
    public static class WorkflowCatalogBuilder
    {
        public const string Engine = "Elsa 3.9.0";

        public static JsonObject Build(IActivityRegistry registry, IApiSerializer serializer, IExpressionDescriptorRegistry? expressions = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(serializer);
            var descriptors = registry.ListAll()
                .Where(x => x.IsBrowsable)
                .GroupBy(x => x.TypeName, StringComparer.Ordinal)
                .Select(x => x.MaxBy(d => d.Version)!)
                .OrderBy(x => x.Kind == ActivityKind.Trigger ? 0 : 1)
                .ThenBy(x => x.TypeName.StartsWith("RIoT2.", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(x => x.Category, StringComparer.Ordinal)
                .ThenBy(x => x.DisplayName ?? x.TypeName, StringComparer.Ordinal);

            var activities = new JsonArray();
            foreach (var descriptor in descriptors)
            {
                var json = JsonNode.Parse(serializer.Serialize(descriptor))!.AsObject();
                var outcomes = descriptor.Ports.Where(x => x.Type == PortType.Flow && x.IsBrowsable).Select(x => x.Name).ToArray();
                var slots = descriptor.Ports.Where(x => x.Type == PortType.Embedded && x.IsBrowsable)
                    .Select(x => (JsonNode)new JsonObject { ["key"] = Camel(x.Name), ["displayName"] = x.DisplayName ?? x.Name })
                    .ToArray();
                activities.Add(new JsonObject
                {
                    ["type"] = descriptor.TypeName,
                    ["version"] = descriptor.Version,
                    ["category"] = descriptor.Category,
                    ["displayName"] = descriptor.DisplayName ?? descriptor.Name,
                    ["description"] = descriptor.Description,
                    ["kind"] = descriptor.Kind == ActivityKind.Trigger ? "Trigger" : "Action",
                    ["isTerminal"] = descriptor.IsTerminal,
                    ["isStart"] = descriptor.IsStart,
                    ["outcomes"] = new JsonArray((outcomes.Length > 0 ? outcomes : ["Done"]).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
                    ["slots"] = new JsonArray(slots),
                    ["inputs"] = new JsonArray((json["inputs"]?.AsArray() ?? []).Where(x => x?["isBrowsable"]?.GetValue<bool>() != false)
                        .OrderBy(x => x?["order"]?.GetValue<float>() ?? 0)
                        .Select(x => (JsonNode)new JsonObject
                        {
                            ["key"] = Camel(x!["name"]!.GetValue<string>()),
                            ["displayName"] = x["displayName"]?.DeepClone(),
                            ["description"] = x["description"]?.DeepClone(),
                            ["category"] = x["category"]?.DeepClone(),
                            ["typeName"] = x["typeName"]?.DeepClone(),
                            ["uiHint"] = x["uiHint"]?.DeepClone(),
                            ["isWrapped"] = x["isWrapped"]?.DeepClone(),
                            ["defaultSyntax"] = x["defaultSyntax"]?.DeepClone(),
                            ["defaultValue"] = x["defaultValue"]?.DeepClone(),
                            ["isReadOnly"] = x["isReadOnly"]?.DeepClone(),
                            ["options"] = x["uiSpecifications"]?.DeepClone()
                        }).ToArray()),
                    ["outputs"] = new JsonArray((json["outputs"]?.AsArray() ?? []).Where(x => x?["isBrowsable"]?.GetValue<bool>() != false)
                        .Select(x => (JsonNode)new JsonObject
                        {
                            ["key"] = Camel(x!["name"]!.GetValue<string>()),
                            ["displayName"] = x["displayName"]?.DeepClone(),
                            ["typeName"] = x["typeName"]?.DeepClone()
                        }).ToArray())
                });
            }

            // Literal (a plain value) is not browsable in Elsa but is every input's default syntax.
            var languages = (expressions?.ListAll().Where(x => x.IsBrowsable).Select(x => x.Type).ToArray()
                ?? ["JavaScript", "Liquid", "Object", "Variable"]).Append("Literal").Distinct(StringComparer.Ordinal);
            return new JsonObject
            {
                ["engine"] = Engine,
                ["languages"] = new JsonArray(languages.OrderBy(x => x, StringComparer.Ordinal).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
                ["activities"] = activities
            };
        }

        private static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
