namespace Aeges.Runners;

/// <summary>
/// Defines deterministic behaviors supported by the mock runner.
/// </summary>
public enum MockRunnerBehavior
{
    /// <summary>
    /// The mock runner succeeds.
    /// </summary>
    Succeed = 0,

    /// <summary>
    /// The mock runner fails.
    /// </summary>
    Fail = 1,

    /// <summary>
    /// The mock runner reports a timeout.
    /// </summary>
    TimeOut = 2,

    /// <summary>
    /// The mock runner reports that approval is required.
    /// </summary>
    RequireApproval = 3,
}
