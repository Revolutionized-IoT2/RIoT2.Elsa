using System.Text.Json;
using Elsa.Workflows.Helpers;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Filters;
using Microsoft.Extensions.Options;
using RIoT2.Elsa.Server.RIoT.Activities;
using RIoT2.Elsa.Server.RIoT.Services;

namespace RIoT2.Elsa.Server.RIoT.Automation
{
    /// <summary>
    /// Fires the <c>RIoT Schedule</c> triggers and bookmarks: it reads their schedules from Elsa's trigger and bookmark
    /// stores, and when one is due sends its key as the stimulus, with the input <c>FiredAt</c>. It checks at the next due
    /// time, and at least every 30 seconds for new schedules. Times are in the host's time zone (<c>TZ</c>). A time missed
    /// while Elsa was stopped is not fired afterwards, nor one more than <see cref="MaxCatchUp"/> late (a clock jump, a
    /// long store outage); when the clock goes back, nothing fires twice.
    /// </summary>
    public sealed class ScheduleService : BackgroundService
    {
        public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);
        /// <summary>How late a schedule may still fire; older due times are skipped.</summary>
        public static readonly TimeSpan MaxCatchUp = TimeSpan.FromSeconds(60);

        private readonly IServiceScopeFactory _scopes;
        private readonly GeoLocation? _location;
        private readonly TimeZoneInfo _zone;
        private readonly TimeProvider _time;
        private readonly ILogger<ScheduleService> _logger;
        private readonly HashSet<string> _warned = new(StringComparer.Ordinal);
        private readonly IHostApplicationLifetime? _lifetime;

        public ScheduleService(IServiceScopeFactory scopes, IOptions<WorkflowOptions> options, TimeProvider time, IHostApplicationLifetime lifetime, ILogger<ScheduleService> logger)
            : this(scopes, options.Value.Location, TimeZoneInfo.Local, time, logger)
        {
            _lifetime = lifetime;
        }

        public ScheduleService(IServiceScopeFactory scopes, GeoLocation? location, TimeZoneInfo zone, TimeProvider time, ILogger<ScheduleService> logger)
        {
            _scopes = scopes;
            _location = location;
            _zone = zone;
            _time = time;
            _logger = logger;
        }

        public static string StimulusName => ActivityTypeNameHelper.GenerateTypeName<RIoTSchedule>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Elsa creates and migrates its database while the host starts; the trigger store is ready after that.
            if (_lifetime != null)
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using (_lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
                using (stoppingToken.Register(() => started.TrySetCanceled(stoppingToken)))
                {
                    try
                    {
                        await started.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
            var from = _time.GetUtcNow();
            var wait = TimeSpan.FromSeconds(1);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(wait, _time, stoppingToken).ConfigureAwait(false);
                    var to = _time.GetUtcNow();
                    try
                    {
                        wait = await RunOnceAsync(from, to, stoppingToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        // Even after a failed pass, so what was sent isn't sent again; never backwards.
                        if (to > from)
                            from = to;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "The RIoT schedules could not be checked; trying again in {Seconds} s", MaxWait.TotalSeconds);
                    wait = MaxWait;
                }
            }
        }

        /// <summary>
        /// Fires every schedule due after <paramref name="from"/> up to <paramref name="to"/>, at most <see cref="MaxCatchUp"/>
        /// back; returns how long to wait for the next check. A schedule that can't be sent is logged and the others still fire.
        /// </summary>
        public async Task<TimeSpan> RunOnceAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        {
            if (to - from > MaxCatchUp)
                from = to - MaxCatchUp;
            using var scope = _scopes.CreateScope();
            var specs = await LoadAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
            var sender = scope.ServiceProvider.GetRequiredService<IStimulusSender>();
            var next = to + MaxWait;
            foreach (var spec in specs)
            {
                var due = spec.NextAfter(from, _zone, _location);
                if (due != null && due <= to)
                {
                    _logger.LogInformation("Schedule {Schedule} fired for {Due}", spec.Key, due);
                    var metadata = new StimulusMetadata { Input = new Dictionary<string, object> { ["FiredAt"] = due.Value } };
                    try
                    {
                        await sender.SendAsync(StimulusName, spec.Key, metadata, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogError(error, "Schedule {Schedule} could not start its workflows", spec.Key);
                    }
                    due = spec.NextAfter(to, _zone, _location);
                }
                if (due != null && due < next)
                    next = due.Value;
            }
            var wait = next - _time.GetUtcNow();
            return wait < TimeSpan.FromMilliseconds(200) ? TimeSpan.FromMilliseconds(200) : wait > MaxWait ? MaxWait : wait;
        }

        private async Task<IReadOnlyList<ScheduleSpec>> LoadAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var triggers = await services.GetRequiredService<ITriggerStore>()
                .FindManyAsync(new TriggerFilter { Name = StimulusName }, cancellationToken).ConfigureAwait(false);
            foreach (var trigger in triggers)
                AddKey(keys, trigger.Payload);
            var bookmarks = await services.GetRequiredService<IBookmarkStore>()
                .FindManyAsync(new BookmarkFilter { Name = StimulusName }, cancellationToken).ConfigureAwait(false);
            foreach (var bookmark in bookmarks)
                AddKey(keys, bookmark.Payload);

            var specs = new List<ScheduleSpec>();
            foreach (var key in keys)
            {
                if (!ScheduleSpec.TryParseKey(key, out var spec))
                    Warn(key, "Schedule '{Schedule}' is not valid; fix it in the workflow");
                else if (spec!.IsSunBased && _location == null)
                    Warn(key, "Schedule '{Schedule}' needs RIOT2_LATITUDE and RIOT2_LONGITUDE; it doesn't fire");
                else
                    specs.Add(spec);
            }
            return specs;
        }

        private static void AddKey(HashSet<string> keys, object? payload)
        {
            var key = payload switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(key))
                keys.Add(key);
        }

        private void Warn(string key, string message)
        {
            lock (_warned)
            {
                if (!_warned.Add(key))
                    return;
            }
#pragma warning disable CA2254 // The two templates are constants of this class.
            _logger.LogWarning(message, key);
#pragma warning restore CA2254
        }
    }
}
