using System.Reflection;
using Elsa.Workflows;
using RIoT2.Elsa.Workflows.Models;

namespace RIoT2.Elsa.Workflows.UIHints
{
    /// <summary>
    /// The settings of a <c>riot-output-selector</c> input: which kinds of RIoT templates it offers, and whether the
    /// designer shows the value editor (a JavaScript expression that returns the command's value). With
    /// <see cref="ForecastOnly"/> (2.1.0) it offers only reports whose value is a forecast series. The designer reads them
    /// from the activity catalog (<c>options.riot-output-selector</c>) and lists the templates from the orchestrator.
    /// </summary>
    public sealed record RIoTSelectorSettings(IReadOnlyList<TemplateType> TemplateTypes, bool HideEditor, bool ForecastOnly = false);

    /// <summary>A <c>riot-output-selector</c> input's static settings; the template lists come from the orchestrator.</summary>
    public abstract class RIoTSelectorUIHandler(bool hideEditor, bool forecastOnly, params TemplateType[] templateTypes) : IPropertyUIHandler
    {
        /// <summary>The UI hint of every RIoT template selector.</summary>
        public const string UIHint = "riot-output-selector";

        protected RIoTSelectorUIHandler(bool hideEditor, params TemplateType[] templateTypes) : this(hideEditor, false, templateTypes)
        {
        }

        public float Priority => 0;

        public RIoTSelectorSettings Settings { get; } = new(templateTypes, hideEditor, forecastOnly);

        // Plain values, so every serializer writes {"templateTypes":["Report",…],"hideEditor":true}.
        public ValueTask<IDictionary<string, object>> GetUIPropertiesAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken = default)
        {
            var settings = new Dictionary<string, object>
            {
                ["templateTypes"] = Settings.TemplateTypes.Select(x => x.ToString()).ToArray(),
                ["hideEditor"] = Settings.HideEditor
            };
            if (Settings.ForecastOnly)
                settings["forecastOnly"] = true;
            return ValueTask.FromResult<IDictionary<string, object>>(new Dictionary<string, object> { [UIHint] = settings });
        }
    }

    /// <summary>Reports and variables, for <c>RIoT Trigger</c>.</summary>
    public sealed class RIoTTriggerOptionsProvider() : RIoTSelectorUIHandler(true, TemplateType.Report, TemplateType.Variable);

    /// <summary>Commands, variables and reports, for <c>RIoT Data</c>.</summary>
    public sealed class RIoTDataOptionsProvider() : RIoTSelectorUIHandler(true, TemplateType.Command, TemplateType.Variable, TemplateType.Report);

    /// <summary>Commands and variables with the value editor, for <c>RIoT Output</c> and <c>RIoT Command with confirmation</c>.</summary>
    public sealed class RIoTOutputOptionsProvider() : RIoTSelectorUIHandler(false, TemplateType.Command, TemplateType.Variable);

    /// <summary>Commands, for <c>RIoT Notification</c>.</summary>
    public sealed class RIoTCommandOptionsProvider() : RIoTSelectorUIHandler(true, TemplateType.Command);

    /// <summary>Reports, for <c>RIoT Wait for state</c>.</summary>
    public sealed class RIoTReportOptionsProvider() : RIoTSelectorUIHandler(true, TemplateType.Report);

    /// <summary>Forecast reports only, for <c>RIoT Forecast</c> (2.1.0).</summary>
    public sealed class RIoTForecastOptionsProvider() : RIoTSelectorUIHandler(true, true, TemplateType.Report);
}
