using System.Collections.Concurrent;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    /// <summary>
    /// Runs a workflow trigger once per message id (design 7.1). The orchestrator's outbox delivers at
    /// least once, so a trigger whose answer was lost arrives again with the same id. An id seen in the
    /// last ten minutes is not run again. A request that arrives while the first run is still in
    /// progress waits for that run and shares its outcome. A run that fails is forgotten, so the
    /// redelivery runs it again.
    /// </summary>
    public sealed class TriggerDeduplicator
    {
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(1);

        private readonly ConcurrentDictionary<string, Entry> _seen = new(StringComparer.Ordinal);
        private readonly TimeProvider _time;
        private long _nextPruneTicks;

        public TriggerDeduplicator() : this(TimeProvider.System) { }

        public TriggerDeduplicator(TimeProvider time)
        {
            _time = time;
        }

        /// <summary>The number of ids currently remembered.</summary>
        public int Count => _seen.Count;

        /// <summary>
        /// Runs <paramref name="run"/> unless <paramref name="messageId"/> was already run in the last
        /// ten minutes. An empty id (an older orchestrator) always runs.
        /// </summary>
        /// <returns><c>true</c> when this call ran the trigger, <c>false</c> for a duplicate.</returns>
        public async Task<bool> RunOnceAsync(string? messageId, Func<Task> run)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (string.IsNullOrEmpty(messageId))
            {
                await run();
                return true;
            }

            var now = _time.GetUtcNow();
            Prune(now);
            var entry = new Entry(now);
            while (true)
            {
                var existing = _seen.GetOrAdd(messageId, entry);
                if (ReferenceEquals(existing, entry))
                    break;
                if (!existing.IsExpired(now))
                {
                    // Rethrows when the first run failed; the sender then retries later.
                    await existing.Completion.Task;
                    return false;
                }
                if (_seen.TryUpdate(messageId, entry, existing))
                    break;
            }

            try
            {
                await run();
                entry.Completion.SetResult();
                return true;
            }
            catch (Exception x)
            {
                _seen.TryRemove(new KeyValuePair<string, Entry>(messageId, entry));
                entry.Completion.SetException(x);
                // Observed here so a failure that no duplicate waited for is not reported as unobserved.
                _ = entry.Completion.Task.Exception;
                throw;
            }
        }

        private void Prune(DateTimeOffset now)
        {
            var next = Interlocked.Read(ref _nextPruneTicks);
            if (now.UtcTicks < next || Interlocked.CompareExchange(ref _nextPruneTicks, now.Add(PruneInterval).UtcTicks, next) != next)
                return;
            foreach (var (id, entry) in _seen)
                if (entry.IsExpired(now))
                    _seen.TryRemove(new KeyValuePair<string, Entry>(id, entry));
        }

        private sealed class Entry(DateTimeOffset seen)
        {
            public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // A run still in progress never expires; its duplicates wait for it.
            public bool IsExpired(DateTimeOffset now) => Completion.Task.IsCompleted && now - seen >= Window;
        }
    }
}
