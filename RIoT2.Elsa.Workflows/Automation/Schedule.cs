using System.Globalization;
using System.Text.RegularExpressions;

namespace RIoT2.Elsa.Workflows.Automation
{
    /// <summary>Where the sun is calculated for: <c>RIOT2_LATITUDE</c> and <c>RIOT2_LONGITUDE</c> in degrees (north and east positive).</summary>
    public sealed record GeoLocation(double Latitude, double Longitude)
    {
        public const string LatitudeVariable = "RIOT2_LATITUDE";
        public const string LongitudeVariable = "RIOT2_LONGITUDE";

        /// <summary>
        /// Reads <c>RIOT2_LATITUDE</c> and <c>RIOT2_LONGITUDE</c>, both or neither, the same way in every workflow host.
        /// </summary>
        /// <param name="read">Reads a setting; null or blank when it is unset.</param>
        /// <param name="addError">Collects a problem; the host reports them together.</param>
        /// <returns>The location, or null when neither is set or a value is invalid.</returns>
        public static GeoLocation? Read(Func<string, string?> read, Action<string> addError)
        {
            ArgumentNullException.ThrowIfNull(read);
            ArgumentNullException.ThrowIfNull(addError);
            var latitude = Degrees(read, addError, LatitudeVariable, 90);
            var longitude = Degrees(read, addError, LongitudeVariable, 180);
            if (latitude.Set != longitude.Set)
            {
                addError($"{LatitudeVariable} and {LongitudeVariable} must be set together");
                return null;
            }
            return latitude.Value is { } lat && longitude.Value is { } lon ? new GeoLocation(lat, lon) : null;
        }

        private static (bool Set, double? Value) Degrees(Func<string, string?> read, Action<string> addError, string name, double limit)
        {
            var text = read(name);
            if (string.IsNullOrWhiteSpace(text))
                return (false, null);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || Math.Abs(value) > limit)
            {
                addError($"{name} must be a number of degrees from -{limit} to {limit}, with a decimal point");
                return (true, null);
            }
            return (true, value);
        }
    }

    /// <summary>
    /// Sunrise and sunset with the sun's upper edge at the horizon and standard refraction (zenith 90.833°), by the
    /// algorithm of the Almanac for Computers (1990). It is within a minute or two outside the polar regions.
    /// </summary>
    public static class SunCalculator
    {
        private const double Zenith = 90.833;

        /// <summary>The sunrise (or sunset) on the local date <paramref name="date"/>, or <c>null</c> when the sun doesn't rise (or set) that day.</summary>
        public static DateTimeOffset? Event(DateOnly date, TimeZoneInfo zone, GeoLocation location, bool sunrise)
        {
            ArgumentNullException.ThrowIfNull(zone);
            ArgumentNullException.ThrowIfNull(location);
            // The event of a UTC date may fall on the next or previous local date far from Greenwich.
            for (var offset = -1; offset <= 1; offset++)
            {
                var utc = UtcEvent(date.AddDays(offset), location.Latitude, location.Longitude, sunrise);
                if (utc != null && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc.Value, zone).DateTime) == date)
                    return utc;
            }
            return null;
        }

        private static DateTimeOffset? UtcEvent(DateOnly date, double latitude, double longitude, bool sunrise)
        {
            var longitudeHour = longitude / 15;
            var t = date.DayOfYear + ((sunrise ? 6 : 18) - longitudeHour) / 24;
            var meanAnomaly = 0.9856 * t - 3.289;
            var trueLongitude = Normalize(meanAnomaly + 1.916 * Sin(meanAnomaly) + 0.020 * Sin(2 * meanAnomaly) + 282.634, 360);
            var rightAscension = Normalize(Atan(0.91764 * Tan(trueLongitude)), 360);
            rightAscension += Math.Floor(trueLongitude / 90) * 90 - Math.Floor(rightAscension / 90) * 90;
            rightAscension /= 15;
            var sinDeclination = 0.39782 * Sin(trueLongitude);
            var cosDeclination = Math.Cos(Math.Asin(sinDeclination));
            var cosHourAngle = (Cos(Zenith) - sinDeclination * Sin(latitude)) / (cosDeclination * Cos(latitude));
            if (cosHourAngle is > 1 or < -1 || double.IsNaN(cosHourAngle))
                return null;
            var hourAngle = (sunrise ? 360 - Acos(cosHourAngle) : Acos(cosHourAngle)) / 15;
            var localMeanTime = hourAngle + rightAscension - 0.06571 * t - 6.622;
            var universalTime = Normalize(localMeanTime - longitudeHour, 24);
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(universalTime);
        }

        private static double Normalize(double value, double range) => ((value % range) + range) % range;
        private static double Sin(double degrees) => Math.Sin(degrees * Math.PI / 180);
        private static double Cos(double degrees) => Math.Cos(degrees * Math.PI / 180);
        private static double Tan(double degrees) => Math.Tan(degrees * Math.PI / 180);
        private static double Atan(double value) => Math.Atan(value) * 180 / Math.PI;
        private static double Acos(double value) => Math.Acos(value) * 180 / Math.PI;
    }

    public enum ScheduleKind
    {
        Time,
        Sunrise,
        Sunset
    }

    /// <summary>
    /// When a <c>RIoT Schedule</c> fires: a local time of day (<c>07:30</c>) or sunrise or sunset with an optional offset in
    /// minutes (<c>sunset-30</c>), on every day or on chosen days. <see cref="Key"/> is its canonical text, which is also the
    /// stimulus the schedule service sends: <c>07:30</c>, <c>sunrise+15 sat,sun</c>.
    /// </summary>
    public sealed partial class ScheduleSpec
    {
        private static readonly string[] DayNames = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
        // Monday first, as the key lists the days.
        private static readonly DayOfWeek[] Week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

        private ScheduleSpec(ScheduleKind kind, TimeOnly time, int offsetMinutes, IReadOnlySet<DayOfWeek> days)
        {
            Kind = kind;
            Time = time;
            OffsetMinutes = offsetMinutes;
            Days = days;
            var at = kind switch
            {
                ScheduleKind.Time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
                _ => (kind == ScheduleKind.Sunrise ? "sunrise" : "sunset") + (offsetMinutes == 0 ? "" : offsetMinutes.ToString("+0;-0", CultureInfo.InvariantCulture))
            };
            Key = days.Count == 7 ? at : at + " " + string.Join(",", Week.Where(days.Contains).Select(d => DayNames[(int)d]));
        }

        public ScheduleKind Kind { get; }
        public TimeOnly Time { get; }
        public int OffsetMinutes { get; }
        public IReadOnlySet<DayOfWeek> Days { get; }
        public string Key { get; }

        public bool IsSunBased => Kind != ScheduleKind.Time;

        public override string ToString() => Key;

        /// <summary>
        /// Reads a schedule. <paramref name="at"/>: <c>HH:mm</c>, <c>sunrise</c> or <c>sunset</c>, optionally with
        /// <c>+</c>/<c>-</c> minutes (up to 720). <paramref name="days"/>: empty, <c>daily</c>, <c>weekdays</c>,
        /// <c>weekends</c>, or day names separated by commas (<c>mon,wed,fri</c>).
        /// </summary>
        public static bool TryParse(string? at, string? days, out ScheduleSpec? spec, out string? error)
        {
            spec = null;
            var match = AtPattern().Match((at ?? "").Trim().ToLowerInvariant());
            if (!match.Success)
            {
                error = $"'{at}' is not a time (HH:mm), sunrise or sunset (sunset-30)";
                return false;
            }
            if (!TryParseDays(days, out var daySet, out error))
                return false;
            if (match.Groups["hour"].Success)
            {
                var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
                var minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
                if (hour > 23 || minute > 59)
                {
                    error = $"'{at}' is not a time of day";
                    return false;
                }
                spec = new ScheduleSpec(ScheduleKind.Time, new TimeOnly(hour, minute), 0, daySet);
                return true;
            }
            var offset = 0;
            if (match.Groups["offset"].Success)
                offset = int.Parse(match.Groups["offset"].Value.Replace(" ", "", StringComparison.Ordinal), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            if (Math.Abs(offset) > 720)
            {
                error = $"'{at}': the offset is at most 720 minutes";
                return false;
            }
            spec = new ScheduleSpec(match.Groups["sun"].Value == "sunrise" ? ScheduleKind.Sunrise : ScheduleKind.Sunset, default, offset, daySet);
            return true;
        }

        /// <summary>Reads a <see cref="Key"/>.</summary>
        public static bool TryParseKey(string? key, out ScheduleSpec? spec)
        {
            var parts = (key ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return TryParse(parts.Length > 0 ? parts[0] : "", parts.Length > 1 ? parts[1] : "", out spec, out _);
        }

        /// <summary>
        /// The first time after <paramref name="after"/> this schedule fires, in <paramref name="zone"/>; <c>null</c> for a
        /// sun-based schedule without a location, or when the sun doesn't rise (set) on any chosen day within a year.
        /// A time that a daylight saving change skips fires at the end of the gap; a repeated time fires once, the first time.
        /// </summary>
        public DateTimeOffset? NextAfter(DateTimeOffset after, TimeZoneInfo zone, GeoLocation? location)
        {
            ArgumentNullException.ThrowIfNull(zone);
            if (IsSunBased && location == null)
                return null;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
            for (var day = today.AddDays(-1); day <= today.AddDays(370); day = day.AddDays(1))
            {
                if (!Days.Contains(day.DayOfWeek))
                    continue;
                var occurrence = Kind == ScheduleKind.Time
                    ? AtLocalTime(day, Time, zone)
                    : SunCalculator.Event(day, zone, location!, Kind == ScheduleKind.Sunrise)?.AddMinutes(OffsetMinutes);
                if (occurrence > after)
                    return occurrence;
            }
            return null;
        }

        private static DateTimeOffset AtLocalTime(DateOnly day, TimeOnly time, TimeZoneInfo zone)
        {
            var local = day.ToDateTime(time, DateTimeKind.Unspecified);
            while (zone.IsInvalidTime(local))
                local = local.AddMinutes(1);
            var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
            return new DateTimeOffset(local, offset);
        }

        private static bool TryParseDays(string? text, out IReadOnlySet<DayOfWeek> days, out string? error)
        {
            var set = new HashSet<DayOfWeek>();
            error = null;
            foreach (var raw in (text ?? "").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim().ToLowerInvariant();
                switch (name)
                {
                    case "daily" or "all" or "every":
                        set.UnionWith(Week);
                        continue;
                    case "weekdays":
                        set.UnionWith(Week[..5]);
                        continue;
                    case "weekends":
                        set.UnionWith(Week[5..]);
                        continue;
                }
                var index = Array.FindIndex(DayNames, d => name.StartsWith(d, StringComparison.Ordinal) && name.Length <= 9);
                if (index < 0)
                {
                    error = $"'{raw}' is not a day; use mon … sun, weekdays, weekends or daily";
                    days = set;
                    return false;
                }
                set.Add((DayOfWeek)index);
            }
            if (set.Count == 0)
                set.UnionWith(Week);
            days = set;
            return true;
        }

        [GeneratedRegex(@"^(?:(?<hour>\d{1,2}):(?<minute>\d{2})|(?<sun>sunrise|sunset)\s*(?<offset>[+-]\s*\d{1,4})?)$")]
        private static partial Regex AtPattern();
    }
}
