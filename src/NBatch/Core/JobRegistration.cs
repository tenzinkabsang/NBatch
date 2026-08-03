using Cronos;

namespace NBatch.Core;

/// <summary>
/// Represents a registered job and allows configuring its execution schedule.
/// Returned by <see cref="NBatchBuilder.AddJob(string, Action{JobBuilder})"/>.
/// <para>
/// Jobs without a schedule are on-demand only — triggered via <see cref="IJobRunner.RunAsync"/>.
/// The last schedule call wins: <see cref="RunOnce"/>, <see cref="RunEvery(TimeSpan, bool)"/>,
/// and <see cref="RunOnCron"/> each replace any previously configured schedule.
/// </para>
/// </summary>
public sealed class JobRegistration
{
    private bool _runOnce;
    private TimeSpan _interval;
    private CronExpression? _cron;

    internal string JobName { get; }
    internal bool IsScheduled => _runOnce || _interval > TimeSpan.Zero || _cron is not null;
    internal bool IsRunOnce => _runOnce;
    internal TimeSpan Interval => _interval;
    internal bool RunImmediately { get; private set; } = true;
    internal CronExpression? CronSchedule => _cron;
    internal TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;

    internal JobRegistration(string jobName)
    {
        JobName = jobName;
    }

    /// <summary>
    /// Runs the job once when the application starts, then the worker exits.
    /// The job remains available on-demand via <see cref="IJobRunner.RunAsync"/>.
    /// </summary>
    public JobRegistration RunOnce()
    {
        _runOnce = true;
        _interval = TimeSpan.Zero;
        _cron = null;
        return this;
    }

    /// <summary>
    /// Repeats the job on a fixed interval, measured from the <em>completion</em> of the
    /// previous run, so runs never overlap.
    /// The job remains available on-demand via <see cref="IJobRunner.RunAsync"/>.
    /// </summary>
    /// <param name="interval">Time to wait after each run completes before starting the next run.</param>
    /// <param name="runImmediately">
    /// When true (default), the first run starts as soon as the application starts;
    /// when false, the worker waits one interval before the first run.
    /// </param>
    public JobRegistration RunEvery(TimeSpan interval, bool runImmediately = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        _interval = interval;
        _runOnce = false;
        _cron = null;
        RunImmediately = runImmediately;
        return this;
    }

    /// <summary>
    /// Runs the job on a standard 5-field cron schedule (minute granularity).
    /// Occurrences that pass while a run is still in progress are skipped —
    /// the next run is always computed from the current time.
    /// </summary>
    /// <param name="cronExpression">A standard cron expression, e.g. <c>"0 2 * * *"</c> for 02:00 daily.</param>
    /// <param name="timeZone">Time zone the schedule is evaluated in. Defaults to UTC.</param>
    /// <exception cref="ArgumentException"><paramref name="cronExpression"/> is not a valid cron expression.</exception>
    public JobRegistration RunOnCron(string cronExpression, TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        try
        {
            _cron = CronExpression.Parse(cronExpression);
        }
        catch (CronFormatException ex)
        {
            throw new ArgumentException($"Invalid cron expression '{cronExpression}'.", nameof(cronExpression), ex);
        }

        _runOnce = false;
        _interval = TimeSpan.Zero;
        TimeZone = timeZone ?? TimeZoneInfo.Utc;
        return this;
    }
}
