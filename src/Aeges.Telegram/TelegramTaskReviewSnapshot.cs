using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Describes the review-facing task state shown through the Telegram transport.
/// </summary>
/// <param name="Task">The task being reviewed.</param>
/// <param name="Iterations">The task iterations recorded by the runtime.</param>
/// <param name="Artifacts">The durable artifacts registered for the task.</param>
/// <param name="RunnerExecutions">The runner executions recorded for the task.</param>
/// <param name="LatestRunnerResponse">A short human-readable runner response preview, when available.</param>
public sealed record TelegramTaskReviewSnapshot(
    RuntimeTask Task,
    IReadOnlyList<TaskIteration> Iterations,
    IReadOnlyList<RuntimeArtifact> Artifacts,
    IReadOnlyList<RuntimeRunnerExecution> RunnerExecutions,
    string? LatestRunnerResponse);
