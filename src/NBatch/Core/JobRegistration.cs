namespace NBatch.Core;

/// <summary>
/// Represents a registered job and allows configuring its execution schedule.
/// Returned by <see cref="NBatchBuilder.AddJob(string, Action{JobBuilder})"/>.
/// <para>
/// Jobs without a schedule are on-demand only — triggered via <see cref="IJobRunner.RunAsync"/>.
/// </para>
/// </summary>
public sealed class JobRegistration
{
    private bool _runOnce;
    private TimeSpan _interval;

    internal string JobName { get; }
    internal bool IsScheduled => _runOnce || _interval > TimeSpan.Zero;
    internal bool IsRunOnce => _runOnce;
    internal TimeSpan Interval => _interval;

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
        return this;
    }

    /// <summary>
    /// Runs the job immediately on startup, then repeats after each <paramref name="interval"/>.
    /// The interval is measured from the <em>completion</em> of the previous run, so runs never overlap.
    /// The job remains available on-demand via <see cref="IJobRunner.RunAsync"/>.
    /// </summary>
    /// <param name="interval">Time to wait after each run completes before starting the next run.</param>
    public JobRegistration RunEvery(TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        _interval = interval;
        _runOnce = false;
        return this;
    }
}
