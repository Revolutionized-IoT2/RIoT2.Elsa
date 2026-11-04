using System.Text.Json;
using System.Text.Json.Nodes;

namespace RIoT2.Elsa.Workflows.Automation
{
    /// <summary>Moves values between workflow expressions (JavaScript objects, lists, JSON text) and JSON.</summary>
    internal static class ActivityJson
    {
        /// <summary>A workflow value as JSON; JSON text is parsed, other text stays text.</summary>
        public static JsonNode? ToNode(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case JsonNode node:
                    return node.DeepClone();
                case JsonElement element:
                    return JsonNode.Parse(element.GetRawText());
                case string text:
                    var trimmed = text.Trim();
                    if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
                    {
                        try
                        {
                            return JsonNode.Parse(trimmed);
                        }
                        catch (JsonException)
                        {
                        }
                    }
                    return JsonValue.Create(text);
                default:
                    return JsonSerializer.SerializeToNode(value);
            }
        }

        /// <summary>JSON as the plain dictionaries, lists and values later steps (JavaScript among them) read.</summary>
        public static object? ToObject(JsonNode? node) => node switch
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
