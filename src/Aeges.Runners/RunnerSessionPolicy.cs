namespace Aeges.Runners;

/// <summary>
/// Defines how a runner should handle external session continuity.
/// </summary>
public enum RunnerSessionPolicy
{
    /// <summary>
    /// Starts a new external runner session for the execution.
    /// </summary>
    NewSession = 0,

    /// <summary>
    /// Resumes an explicit external runner session.
    /// </summary>
    ResumeSession = 1,
}
