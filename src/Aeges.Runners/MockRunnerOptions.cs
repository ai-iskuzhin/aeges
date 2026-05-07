using Aeges.Core;

namespace Aeges.Runners;

/// <summary>
/// Configures deterministic mock runner behavior.
/// </summary>
/// <param name="RunnerId">The mock runner identifier. The default identifier is used when omitted.</param>
/// <param name="Behavior">The behavior the mock runner should report.</param>
/// <param name="ExecutionDelay">The simulated execution delay.</param>
/// <param name="ErrorSummary">The error or approval summary used for non-success results.</param>
public sealed record MockRunnerOptions(
    RunnerId? RunnerId = null,
    MockRunnerBehavior Behavior = MockRunnerBehavior.Succeed,
    TimeSpan? ExecutionDelay = null,
    string? ErrorSummary = null);
