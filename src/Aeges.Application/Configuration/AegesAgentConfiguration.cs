namespace Aeges.Application.Configuration;

/// <summary>
/// Represents local agent runtime configuration.
/// </summary>
public sealed class AegesAgentConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of project-isolated tasks the local agent may run in parallel.
    /// </summary>
    public int MaxParallelTasks { get; set; } = 1;
}
