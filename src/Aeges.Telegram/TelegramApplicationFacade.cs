using Aeges.Application;
using Aeges.Application.Approvals;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.Tasks;
using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Application-service backed implementation of <see cref="ITelegramApplicationFacade"/>.
/// </summary>
public sealed class TelegramApplicationFacade : ITelegramApplicationFacade
{
    private readonly ProjectService projectService;
    private readonly MachineService machineService;
    private readonly TaskService taskService;
    private readonly ApprovalService approvalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramApplicationFacade"/> class.
    /// </summary>
    /// <param name="projectService">The project application service.</param>
    /// <param name="machineService">The machine application service.</param>
    /// <param name="taskService">The task application service.</param>
    /// <param name="approvalService">The approval application service.</param>
    public TelegramApplicationFacade(
        ProjectService projectService,
        MachineService machineService,
        TaskService taskService,
        ApprovalService approvalService)
    {
        this.projectService = projectService;
        this.machineService = machineService;
        this.taskService = taskService;
        this.approvalService = approvalService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken) =>
        await projectService.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken) =>
        await machineService.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await taskService.ListByStatusAsync(RuntimeTaskStatus.Queued, limit, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingApprovalsAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await approvalService.ListPendingAsync(limit, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTask>> GetTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await taskService.GetAsync(taskId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTask>> CancelTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await taskService.CancelAsync(taskId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<ApprovalRequest>> GetApprovalAsync(
        ApprovalId approvalId,
        CancellationToken cancellationToken) =>
        await approvalService.GetAsync(approvalId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<ApprovalRequest>> ApproveApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken) =>
        await approvalService.ApproveAsync(new ResolveApprovalRequest(approvalId, resolvedBy), cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<ApprovalRequest>> RejectApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken) =>
        await approvalService.RejectAsync(new ResolveApprovalRequest(approvalId, resolvedBy), cancellationToken);
}
