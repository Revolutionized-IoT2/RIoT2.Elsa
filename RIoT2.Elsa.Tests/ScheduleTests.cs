using Elsa.Extensions;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Entities;
using Elsa.Workflows.Runtime.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RIoT2.Elsa.Workflows.Automation;

namespace RIoT2.Elsa.Tests;

/// <summary><c>RIoT Schedule</c> (I48): the schedule text, local times across daylight saving, sunrise and sunset, and the schedule service.</summary>
[TestClass]
public class ScheduleTests
{
    private static readonly TimeZoneInfo Helsinki = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");
    private static readonly GeoLocation HelsinkiCentre = new(60.1699, 24.9384);

    [TestMethod]
    [DataRow("7:30", "", "07:30")]
    [DataRow(" Sunrise ", "daily", "sunrise")]
    [DataRow("sunset - 30", "sun, sat", "sunset-30 sat,sun")]
    [DataRow("SUNRISE+15", "weekdays", "sunrise+15 mon,tue,wed,thu,fri")]
    [DataRow("23:59", "weekends,monday", "23:59 mon,sat,sun")]
    public void SchedulesHaveACanonicalKey(string at, string days, string key)
    {
        Assert.IsTrue(ScheduleSpec.TryParse(at, days, out var spec, out var error), error);
        Assert.AreEqual(key, spec!.Key);
        Assert.IsTrue(ScheduleSpec.TryParseKey(key, out var again));
        Assert.AreEqual(key, again!.Key);
    }

    [TestMethod]
    [DataRow("24:00", "")]
    [DataRow("7.30", "")]
    [DataRow("noon", "")]
    [DataRow("sunset+721", "")]
    [DataRow("07:00", "someday")]
    public void InvalidSchedulesAreRejected(string at, string days)
    {
        Assert.IsFalse(ScheduleSpec.TryParse(at, days, out _, out var error));
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void ATimeOfDayFiresOnTheChosenDaysInLocalTime()
    {
        ScheduleSpec.TryParse("07:30", "mon", out var spec, out _);
        // Tuesday 3 November 2026, 12:00 in Helsinki (EET, +2).
        var next = spec!.NextAfter(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero), Helsinki, null);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 9, 7, 30, 0, TimeSpan.FromHours(2)), next);
        Assert.AreEqual(next!.Value.AddDays(7), spec.NextAfter(next.Value, Helsinki, null), "strictly after");
    }

    [TestMethod]
    public void DaylightSavingGapsAndRepeatsFireOnce()
    {
        ScheduleSpec.TryParse("03:30", "", out var spec, out _);
        // 29 March 2026: 03:00 EET becomes 04:00 EEST, so 03:30 doesn't exist; it fires at 04:00 EEST.
        Assert.AreEqual(new DateTimeOffset(2026, 3, 29, 4, 0, 0, TimeSpan.FromHours(3)),
            spec!.NextAfter(new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero), Helsinki, null));
        // 25 October 2026: 04:00 EEST becomes 03:00 EET, so 03:30 happens twice; only the first fires.
        var first = spec.NextAfter(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero), Helsinki, null);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 25, 3, 30, 0, TimeSpan.FromHours(3)), first);
        Assert.AreEqual(26, spec.NextAfter(first!.Value, Helsinki, null)!.Value.Day);
    }

    [TestMethod]
    [DataRow("Europe/Helsinki", 60.1699, 24.9384, "2026-06-21", "03:54", "22:50")]
    [DataRow("Europe/Helsinki", 60.1699, 24.9384, "2026-12-21", "09:24", "15:13")]
    [DataRow("America/New_York", 40.7128, -74.0060, "2026-07-04", "05:30", "20:31")]
    [DataRow("Asia/Tokyo", 35.6762, 139.6503, "2026-01-01", "06:51", "16:39")]
    public void SunriseAndSunsetAreWithinTwoMinutes(string zoneId, double latitude, double longitude, string date, string sunrise, string sunset)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var day = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture);
        var location = new GeoLocation(latitude, longitude);
        foreach (var (rise, expected) in new[] { (true, sunrise), (false, sunset) })
        {
            var actual = TimeZoneInfo.ConvertTime(SunCalculator.Event(day, zone, location, rise)!.Value, zone);
            var wanted = day.ToDateTime(TimeOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
            Assert.IsLessThanOrEqualTo(2.0, Math.Abs((actual.DateTime - wanted).TotalMinutes), $"{zoneId} {date} {(rise ? "sunrise" : "sunset")}: {actual:HH:mm}");
        }
    }

    [TestMethod]
    public void PolarNightAndMidnightSunAreSkipped()
    {
        var utsjoki = new GeoLocation(69.9, 27.0);
        Assert.IsNull(SunCalculator.Event(new DateOnly(2026, 12, 21), Helsinki, utsjoki, sunrise: true));
        Assert.IsNull(SunCalculator.Event(new DateOnly(2026, 6, 21), Helsinki, utsjoki, sunrise: false));

        ScheduleSpec.TryParse("sunrise", "", out var spec, out _);
        var next = spec!.NextAfter(new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero), Helsinki, utsjoki);
        Assert.IsGreaterThan(new DateTimeOffset(2027, 1, 10, 0, 0, 0, TimeSpan.Zero), next!.Value, "the first sunrise after the polar night");
        Assert.IsNull(spec.NextAfter(DateTimeOffset.UtcNow, Helsinki, null), "no location, no sun");
    }

    [TestMethod]
    public async Task TheServiceFiresDueTriggersAndBookmarksOnce()
    {
        var sender = new RecordingSender();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseWorkflowRuntime());
        services.AddSingleton<IStimulusSender>(sender);
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITriggerStore>().SaveManyAsync(
        [
            Trigger("t1", "07:30"),
            Trigger("t2", "07:30"),
            Trigger("t3", "invalid 25:00|"),
            Trigger("t4", "sunset")
        ]);
        await provider.GetRequiredService<IBookmarkStore>().SaveAsync(new StoredBookmark
        {
            Id = "b1", Name = ScheduleService.StimulusName, Hash = "h", WorkflowInstanceId = "w", Payload = "07:31"
        });
        var time = new ActivityPackTests.ManualTime();
        var service = new ScheduleService(provider.GetRequiredService<IServiceScopeFactory>(), HelsinkiCentre, Helsinki, time, NullLogger<ScheduleService>.Instance);

        // 07:29:50 to 07:30:10 local (EET): the two 07:30 triggers are one schedule.
        var from = new DateTimeOffset(2026, 11, 3, 5, 29, 50, TimeSpan.Zero);
        time.Set(from.AddSeconds(20));
        var wait = await service.RunOnceAsync(from, from.AddSeconds(20), default);
        CollectionAssert.AreEqual(new[] { "07:30" }, sender.Sent.Select(s => s.Stimulus).ToArray());
        Assert.AreEqual(new DateTimeOffset(2026, 11, 3, 5, 30, 0, TimeSpan.Zero), (DateTimeOffset)sender.Sent[0].Input["FiredAt"]);
        Assert.AreEqual(TimeSpan.FromSeconds(30), wait, "the next (07:31) is more than 30 s away");

        // The next window fires the bookmark's 07:31, and nothing twice.
        time.Set(from.AddSeconds(90));
        wait = await service.RunOnceAsync(from.AddSeconds(20), from.AddSeconds(90), default);
        CollectionAssert.AreEqual(new[] { "07:30", "07:31" }, sender.Sent.Select(s => s.Stimulus).ToArray());
        Assert.IsTrue(sender.Sent.All(s => s.Name == ScheduleService.StimulusName));
        Assert.IsLessThanOrEqualTo(ScheduleService.MaxWait, wait);
    }

    [TestMethod]
    public async Task AFailingScheduleDoesntStopTheOthersAndOldTimesAreSkipped()
    {
        var sender = new RecordingSender { FailFor = "07:30" };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseWorkflowRuntime());
        services.AddSingleton<IStimulusSender>(sender);
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITriggerStore>().SaveManyAsync([Trigger("t1", "07:30"), Trigger("t2", "07:30 tue"), Trigger("t3", "03:00")]);
        var time = new ActivityPackTests.ManualTime();
        var service = new ScheduleService(provider.GetRequiredService<IServiceScopeFactory>(), null, Helsinki, time, NullLogger<ScheduleService>.Instance);

        // Tuesday 07:30 local: "07:30" fails to send, "07:30 tue" still fires. 03:00 was hours ago (a clock jump or an
        // outage since then): skipped, not fired late.
        var to = new DateTimeOffset(2026, 11, 3, 5, 30, 5, TimeSpan.Zero);
        time.Set(to);
        await service.RunOnceAsync(to.AddHours(-6), to, default);

        CollectionAssert.AreEqual(new[] { "07:30 tue" }, sender.Sent.Select(s => s.Stimulus).ToArray());
    }

    private static StoredTrigger Trigger(string id, string payload) => new()
    {
        Id = id,
        Name = ScheduleService.StimulusName,
        Payload = payload,
        Hash = id,
        ActivityId = "schedule",
        WorkflowDefinitionId = "definition",
        WorkflowDefinitionVersionId = "definition:1"
    };

    private sealed class RecordingSender : IStimulusSender
    {
        public List<(string Name, object Stimulus, IDictionary<string, object> Input)> Sent { get; } = [];
        public string? FailFor { get; init; }

        public Task<SendStimulusResult> SendAsync(string activityTypeName, object stimulus, StimulusMetadata? metadata = null, CancellationToken cancellationToken = default)
        {
            if (Equals(stimulus, FailFor))
                throw new InvalidOperationException("The store is away");
            Sent.Add((activityTypeName, stimulus, metadata?.Input ?? new Dictionary<string, object>()));
            return Task.FromResult(new SendStimulusResult([]));
        }

        public Task<SendStimulusResult> SendAsync(string stimulusHash, StimulusMetadata? metadata = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
