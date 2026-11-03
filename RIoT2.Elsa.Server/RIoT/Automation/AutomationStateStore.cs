namespace RIoT2.Elsa.Server.RIoT.Automation
{
    /// <summary>
    /// The state the debounce, throttle and threshold activities share between workflow runs, by key. It is kept in
    /// memory: after a restart a debounce lets its pending runs through, a throttle passes the next event, and a
    /// threshold starts unknown. At most <see cref="MaxKeys"/> keys of each kind are kept; past that the oldest go.
    /// </summary>
    public sealed class AutomationStateStore
    {
        public const int MaxKeys = 10_000;

        private readonly TimeProvider _time;
        private readonly object _lock = new();
        private readonly Dictionary<string, (string Token, DateTimeOffset At)> _debounce = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTimeOffset> _throttle = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (ThresholdState State, DateTimeOffset At)> _thresholds = new(StringComparer.Ordinal);

        public AutomationStateStore(TimeProvider time)
        {
            _time = time;
        }

        /// <summary>Starts a debounce wait for <paramref name="key"/>; the returned token is the latest until the next start.</summary>
        public string BeginDebounce(string key)
        {
            var token = Guid.NewGuid().ToString("N");
            lock (_lock)
            {
                Trim(_debounce, x => x.At);
                _debounce[key] = (token, _time.GetUtcNow());
            }
            return token;
        }

        /// <summary>
        /// Ends a debounce wait: <c>true</c> when <paramref name="token"/> is still the latest for the key (or the key is
        /// unknown, after a restart), and then forgets it.
        /// </summary>
        public bool EndDebounce(string key, string token)
        {
            lock (_lock)
            {
                if (!_debounce.TryGetValue(key, out var latest))
                    return true;
                if (latest.Token != token)
                    return false;
                _debounce.Remove(key);
                return true;
            }
        }

        /// <summary>Whether an event for <paramref name="key"/> passes: the first one, and then one per <paramref name="window"/>.</summary>
        public bool TryPass(string key, TimeSpan window)
        {
            var now = _time.GetUtcNow();
            lock (_lock)
            {
                if (_throttle.TryGetValue(key, out var passed) && now - passed < window)
                    return false;
                Trim(_throttle, x => x);
                _throttle[key] = now;
                return true;
            }
        }

        /// <summary>Applies <paramref name="value"/> to the threshold state of <paramref name="key"/> (<see cref="Hysteresis.Next"/>).</summary>
        public (ThresholdState State, string Outcome) Threshold(string key, double value, double lower, double upper)
        {
            lock (_lock)
            {
                var state = _thresholds.TryGetValue(key, out var current) ? current.State : ThresholdState.Unknown;
                var next = Hysteresis.Next(state, value, lower, upper);
                Trim(_thresholds, x => x.At);
                _thresholds[key] = (next.State, _time.GetUtcNow());
                return next;
            }
        }

        private static void Trim<T>(Dictionary<string, T> map, Func<T, DateTimeOffset> at)
        {
            if (map.Count < MaxKeys)
                return;
            foreach (var key in map.OrderBy(x => at(x.Value)).Take(map.Count - MaxKeys + 1).Select(x => x.Key).ToArray())
                map.Remove(key);
        }
    }
}
