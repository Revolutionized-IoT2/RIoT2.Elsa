using System.Globalization;
using System.Text.Json;

namespace RIoT2.Elsa.Server.RIoT.Automation
{
    /// <summary>The comparisons of <c>RIoT Compare</c> and <c>RIoT Wait for state</c>.</summary>
    public enum CompareOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        AtLeast,
        AtMost
    }

    /// <summary>
    /// Reads the values the activities work on: a report (from the trigger, <c>RIoT Data</c> or the orchestrator), a
    /// JSON value or a plain .NET value.
    /// </summary>
    public static class AutomationValues
    {
        /// <summary>The operator names shown in Elsa Studio.</summary>
        public static readonly string[] OperatorNames = ["equals", "not equals", "greater than", "less than", "at least", "at most"];

        /// <summary>
        /// The value as JSON. A report (an object with <c>value</c> and <c>id</c> or <c>timeStamp</c>) is replaced by its
        /// value, so a report and its value can be used alike.
        /// </summary>
        public static JsonElement Read(object? value)
        {
            var element = value switch
            {
                null => default,
                JsonElement json => json,
                _ => JsonSerializer.SerializeToElement(value, value.GetType())
            };
            if (element.ValueKind == JsonValueKind.Object
                && TryGetProperty(element, "value", out var inner)
                && (TryGetProperty(element, "id", out _) || TryGetProperty(element, "timeStamp", out _)))
                return inner;
            return element;
        }

        /// <summary>A number, a boolean as 1 or 0, or a text that is an invariant number; else <c>null</c>.</summary>
        public static double? ToNumber(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };

        /// <summary>A plain .NET value for an activity output: a number, boolean, text, <c>null</c>, or the JSON itself.</summary>
        public static object? ToPlain(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            _ => value.Clone()
        };

        /// <summary>Reads an operator by its name (<see cref="OperatorNames"/>, with or without spaces) or its symbol.</summary>
        public static CompareOperator ParseOperator(string? name) =>
            (name ?? "").Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant() switch
            {
                "" or "equals" or "=" or "==" => CompareOperator.Equals,
                "notequals" or "!=" or "<>" or "≠" => CompareOperator.NotEquals,
                "greaterthan" or ">" => CompareOperator.GreaterThan,
                "lessthan" or "<" => CompareOperator.LessThan,
                "atleast" or ">=" or "≥" => CompareOperator.AtLeast,
                "atmost" or "<=" or "≤" => CompareOperator.AtMost,
                _ => throw new InvalidOperationException($"Unknown comparison '{name}'; use one of: {string.Join(", ", OperatorNames)}.")
            };

        /// <summary>
        /// Compares <paramref name="actual"/> with the text <paramref name="expected"/>: as numbers when both are numbers,
        /// as booleans when the value is a boolean (<c>true</c>/<c>on</c>/<c>1</c>), else as text, ignoring case. The order
        /// comparisons need two numbers; a missing value only equals an empty text or <c>null</c>.
        /// </summary>
        public static bool Compare(JsonElement actual, CompareOperator op, string? expected)
        {
            expected = (expected ?? "").Trim();
            if (op is CompareOperator.Equals or CompareOperator.NotEquals)
                return AreEqual(actual, expected) == (op == CompareOperator.Equals);
            var number = ToNumber(actual);
            if (number == null || !double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var limit))
                return false;
            return op switch
            {
                CompareOperator.GreaterThan => number > limit,
                CompareOperator.LessThan => number < limit,
                CompareOperator.AtLeast => number >= limit,
                _ => number <= limit
            };
        }

        private static bool AreEqual(JsonElement actual, string expected)
        {
            switch (actual.ValueKind)
            {
                case JsonValueKind.Undefined or JsonValueKind.Null:
                    return expected.Length == 0 || expected.Equals("null", StringComparison.OrdinalIgnoreCase);
                case JsonValueKind.True or JsonValueKind.False:
                    return ParseBoolean(expected) is { } flag && flag == (actual.ValueKind == JsonValueKind.True);
                case JsonValueKind.Number:
                    return double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        && Math.Abs(actual.GetDouble() - number) < 1e-9;
                case JsonValueKind.String:
                    return string.Equals(actual.GetString(), expected, StringComparison.OrdinalIgnoreCase);
                default:
                    return string.Equals(actual.GetRawText(), expected, StringComparison.Ordinal);
            }
        }

        private static bool? ParseBoolean(string text) => text.ToLowerInvariant() switch
        {
            "true" or "on" or "1" or "yes" => true,
            "false" or "off" or "0" or "no" => false,
            _ => null
        };

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }

    /// <summary>The state of a <c>RIoT Threshold</c>: unknown until a value leaves the band between the limits.</summary>
    public enum ThresholdState
    {
        Unknown,
        High,
        Low
    }

    public static class Hysteresis
    {
        public const string Above = "Above";
        public const string Below = "Below";
        public const string Unchanged = "Unchanged";

        /// <summary>
        /// The next state and outcome: above <paramref name="upper"/> the state is high, below <paramref name="lower"/> low,
        /// and in between it stays. The outcome is <see cref="Above"/> or <see cref="Below"/> only when the state changes.
        /// </summary>
        public static (ThresholdState State, string Outcome) Next(ThresholdState state, double value, double lower, double upper)
        {
            if (value > upper)
                return (ThresholdState.High, state == ThresholdState.High ? Unchanged : Above);
            if (value < lower)
                return (ThresholdState.Low, state == ThresholdState.Low ? Unchanged : Below);
            return (state, Unchanged);
        }
    }
}
