using Aeges.Core;

namespace Aeges.Runners;

/// <summary>
/// Executes governed discussion prompts through an interchangeable coding-agent runner.
/// </summary>
public interface ITalkRunner
{
    /// <summary>
    /// Gets the stable runner identifier.
    /// </summary>
    RunnerId Id { get; }

    /// <summary>
    /// Sends one discussion prompt to the runner.
    /// </summary>
    /// <param name="request">The governed talk runner request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The runner response.</returns>
    Task<TalkRunnerResult> SendAsync(TalkRunnerRequest request, CancellationToken cancellationToken);
}
