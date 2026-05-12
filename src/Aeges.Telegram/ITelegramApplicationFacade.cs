using Aeges.Application;
using Aeges.Application.Talk;
using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Defines application operations exposed to the Telegram transport.
/// </summary>
public interface ITelegramApplicationFacade
{
    /// <summary>
    /// Lists registered projects.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered projects.</returns>
    Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered project groups.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project groups.</returns>
    Task<IReadOnlyList<RuntimeProjectGroup>> ListProjectGroupsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered projects that can accept new tasks.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The active registered projects.</returns>
    Task<IReadOnlyList<RuntimeProject>> ListActiveProjectsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a project by identifier.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The project lookup result.</returns>
    Task<ApplicationResult<RuntimeProject>> GetProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Archives a project while keeping its tasks and artifacts available for review.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The archive result.</returns>
    Task<ApplicationResult<RuntimeProject>> ArchiveProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends free-form text into the governed talk session for a Telegram chat.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="message">The operator message.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The talk exchange result.</returns>
    Task<ApplicationResult<TalkExchange>> SendTalkMessageAsync(
        long chatId,
        string message,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered machines.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered machines.</returns>
    Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists queued tasks.
    /// </summary>
    /// <param name="limit">The maximum number of tasks to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The queued tasks.</returns>
    Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Lists tasks by lifecycle status.
    /// </summary>
    /// <param name="status">The lifecycle status to list.</param>
    /// <param name="limit">The maximum number of tasks to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching tasks.</returns>
    Task<IReadOnlyList<RuntimeTask>> ListTasksByStatusAsync(
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists tasks for a project by lifecycle status.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="status">The lifecycle status to list.</param>
    /// <param name="limit">The maximum number of tasks to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching project tasks.</returns>
    Task<IReadOnlyList<RuntimeTask>> ListProjectTasksByStatusAsync(
        ProjectId projectId,
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists pending approval requests.
    /// </summary>
    /// <param name="limit">The maximum number of approvals to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The pending approvals.</returns>
    Task<IReadOnlyList<ApprovalRequest>> ListPendingApprovalsAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Gets runner settings exposed through the Telegram settings menu.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The current runner settings.</returns>
    Task<TelegramRunnerSettings> GetRunnerSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the configured Codex sandbox mode.
    /// </summary>
    /// <param name="sandboxMode">The Codex sandbox mode.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated runner settings, or an expected failure.</returns>
    Task<ApplicationResult<TelegramRunnerSettings>> SetCodexSandboxModeAsync(
        string sandboxMode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates whether Codex bypasses its approvals and sandbox.
    /// </summary>
    /// <param name="enabled">A value indicating whether bypass is enabled.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated runner settings, or an expected failure.</returns>
    Task<ApplicationResult<TelegramRunnerSettings>> SetCodexBypassApprovalsAndSandboxAsync(
        bool enabled,
        CancellationToken cancellationToken);

    /// <summary>
    /// Restarts the local agent so updated runtime settings are applied.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The restart result, or an expected failure.</returns>
    Task<ApplicationResult<TelegramAgentRestartResult>> RestartAgentAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts the local agent when it is not already running.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The start result, or an expected failure.</returns>
    Task<ApplicationResult<TelegramAgentRestartResult>> StartAgentAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a queued runtime task.
    /// </summary>
    /// <param name="projectId">The project that owns the task.</param>
    /// <param name="machineId">The machine assigned to process the task.</param>
    /// <param name="title">The task title.</param>
    /// <param name="goal">The task goal.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task creation result.</returns>
    Task<ApplicationResult<RuntimeTask>> CreateTaskAsync(
        ProjectId projectId,
        MachineId machineId,
        string title,
        string goal,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a task by identifier.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task lookup result.</returns>
    Task<ApplicationResult<RuntimeTask>> GetTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets task review details, including iterations, artifacts, runner executions, and an output preview.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task review snapshot, or an expected failure.</returns>
    Task<ApplicationResult<TelegramTaskReviewSnapshot>> GetTaskReviewAsync(
        TaskId taskId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task cancellation result.</returns>
    Task<ApplicationResult<RuntimeTask>> CancelTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Completes a task after review.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task completion result.</returns>
    Task<ApplicationResult<RuntimeTask>> CompleteTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores review feedback and requeues a task for another iteration.
    /// </summary>
    /// <param name="taskId">The reviewed task identifier.</param>
    /// <param name="feedback">The reviewer feedback to include in the next prompt.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The requeued task, or an expected failure.</returns>
    Task<ApplicationResult<RuntimeTask>> ContinueTaskAsync(
        TaskId taskId,
        string feedback,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an approval request by identifier.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The approval lookup result.</returns>
    Task<ApplicationResult<ApprovalRequest>> GetApprovalAsync(ApprovalId approvalId, CancellationToken cancellationToken);

    /// <summary>
    /// Approves a pending approval request.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <param name="resolvedBy">The actor resolving the approval.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The approval resolution result.</returns>
    Task<ApplicationResult<ApprovalRequest>> ApproveApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rejects a pending approval request.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <param name="resolvedBy">The actor resolving the approval.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The approval resolution result.</returns>
    Task<ApplicationResult<ApprovalRequest>> RejectApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken);
}
