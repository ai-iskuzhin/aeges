namespace Aeges.Runners.Codex;

/// <summary>
/// Represents a Codex runner preflight failure when the Codex CLI executable is unavailable.
/// </summary>
public sealed class CodexRunnerUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodexRunnerUnavailableException"/> class.
    /// </summary>
    /// <param name="availability">The failed availability result.</param>
    public CodexRunnerUnavailableException(CodexRunnerAvailability availability)
        : base(availability.Message)
    {
        Availability = availability;
    }

    /// <summary>
    /// Gets the Codex runner availability result.
    /// </summary>
    public CodexRunnerAvailability Availability { get; }
}
