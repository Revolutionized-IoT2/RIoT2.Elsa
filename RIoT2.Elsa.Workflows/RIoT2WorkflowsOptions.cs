using RIoT2.Elsa.Workflows.Automation;

namespace RIoT2.Elsa.Workflows
{
    /// <summary>The workflow engine's settings in any host (<see cref="WorkflowsServiceCollectionExtensions.AddRIoT2Workflows"/>).</summary>
    public sealed class RIoT2WorkflowsOptions
    {
        /// <summary>The default database: <c>Data/elsa.sqlite.db</c> below the working directory.</summary>
        public const string DefaultConnectionString = "Data Source=Data/elsa.sqlite.db;Cache=Shared;";

        /// <summary>Elsa's SQLite database, which holds the definitions, instances, bookmarks and triggers.</summary>
        public string ConnectionString { get; set; } = DefaultConnectionString;

        /// <summary>Where <c>RIoT Schedule</c> calculates sunrise and sunset; <c>null</c>: sun-based schedules don't fire.</summary>
        public GeoLocation? Location { get; set; }

        /// <summary>How long finished workflow instances are kept.</summary>
        public TimeSpan FinishedRetention { get; set; } = TimeSpan.FromHours(24);

        /// <summary>Elsa's HTTP activities (HTTP endpoint, send HTTP request); their endpoints need <c>app.UseWorkflows()</c>.</summary>
        public bool HttpActivities { get; set; } = true;

        /// <summary>
        /// The path below which HTTP endpoint activities listen. Never <c>/api/workflows</c> in the orchestrator, where that
        /// is the workflow management API.
        /// </summary>
        public string HttpBasePath { get; set; } = "/workflows";

        /// <summary>Further HTTP activity settings (the remote workflow host binds its <c>Http</c> configuration section).</summary>
        public Action<global::Elsa.Http.Options.HttpActivityOptions>? ConfigureHttp { get; set; }

        /// <summary>Elsa's C# expressions, which saved workflows may use.</summary>
        public bool CSharpExpressions { get; set; } = true;
    }
}
