using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Approvals;

/// <summary>
/// Coordinates approval request and resolution use cases.
/// </summary>
public sealed class ApprovalService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public ApprovalService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Requests approval and pauses the owning task.
    /// </summary>
    /// <param name="request">The approval request details.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created approval, or an expected failure.</returns>
    public async Task<ApplicationResult<ApprovalRequest>> RequestAsync(
        RequestApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var task = await unitOfWork.Tasks.GetByIdAsync(request.TaskId, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<ApprovalRequest>.Failure("task_not_found", $"Task '{request.TaskId}' was not found.");
        }

        if (request.IterationId is not null)
        {
            var iteration = await unitOfWork.Iterations.GetByIdAsync(request.IterationId.Value, cancellationToken);

            if (iteration is null || iteration.TaskId != request.TaskId)
            {
                return ApplicationResult<ApprovalRequest>.Failure(
                    "iteration_not_found",
                    $"Iteration '{request.IterationId}' was not found for task '{request.TaskId}'.");
            }
        }

        ApprovalRequest approval;

        var now = clock.Now;

        try
        {
            approval = ApprovalRequest.Create(
                request.ApprovalId ?? ApprovalId.New(),
                request.TaskId,
                request.IterationId,
                request.Reason,
                request.RequestedAction,
                now);
            task.WaitForApproval(now);
        }
        catch (InvalidTaskStatusTransitionException exception)
        {
            return ApplicationResult<ApprovalRequest>.Failure("approval_request_not_allowed", exception.Message);
        }
        catch (AegesDomainException exception)
        {
            return ApplicationResult<ApprovalRequest>.Failure("approval_domain_rule_violation", exception.Message);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                await unitOfWork.Approvals.AddAsync(approval, transactionCancellationToken);
                await unitOfWork.Tasks.UpdateAsync(task, transactionCancellationToken);
            },
            cancellationToken);

        return ApplicationResult<ApprovalRequest>.Success(approval);
    }

    /// <summary>
    /// Gets an approval request by identifier.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The approval request, or an expected failure when it does not exist.</returns>
    public async Task<ApplicationResult<ApprovalRequest>> GetAsync(
        ApprovalId approvalId,
        CancellationToken cancellationToken)
    {
        var approval = await unitOfWork.Approvals.GetByIdAsync(approvalId, cancellationToken);

        return approval is null
            ? ApplicationResult<ApprovalRequest>.Failure("approval_not_found", $"Approval request '{approvalId}' was not found.")
            : ApplicationResult<ApprovalRequest>.Success(approval);
    }

    /// <summary>
    /// Lists approval requests belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task approval requests.</returns>
    public async Task<IReadOnlyList<ApprovalRequest>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Approvals.ListByTaskAsync(taskId, cancellationToken);

    /// <summary>
    /// Lists pending approval requests.
    /// </summary>
    /// <param name="limit">The maximum number of approvals to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The pending approval requests.</returns>
    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await unitOfWork.Approvals.ListPendingAsync(limit, cancellationToken);

    /// <summary>
    /// Approves a pending approval request and resumes the task.
    /// </summary>
    /// <param name="request">The resolution request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The resolved approval, or an expected failure.</returns>
    public async Task<ApplicationResult<ApprovalRequest>> ApproveAsync(
        ResolveApprovalRequest request,
        CancellationToken cancellationToken) =>
        await ResolveAsync(
            request,
            static (approval, resolvedBy, now) => approval.Approve(resolvedBy, now),
            static (task, _, now) => task.StartRunning(now),
            cancellationToken);

    /// <summary>
    /// Rejects a pending approval request and fails the task.
    /// </summary>
    /// <param name="request">The resolution request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The resolved approval, or an expected failure.</returns>
    public async Task<ApplicationResult<ApprovalRequest>> RejectAsync(
        ResolveApprovalRequest request,
        CancellationToken cancellationToken) =>
        await ResolveAsync(
            request,
            static (approval, resolvedBy, now) => approval.Reject(resolvedBy, now),
            static (task, approval, now) => task.Fail($"Approval '{approval.Id}' rejected: {approval.Reason}", now),
            cancellationToken);

    /// <summary>
    /// Cancels a pending approval request and cancels the task.
    /// </summary>
    /// <param name="request">The resolution request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The resolved approval, or an expected failure.</returns>
    public async Task<ApplicationResult<ApprovalRequest>> CancelAsync(
        ResolveApprovalRequest request,
        CancellationToken cancellationToken) =>
        await ResolveAsync(
            request,
            static (approval, resolvedBy, now) => approval.Cancel(resolvedBy, now),
            static (task, _, now) => task.Cancel(now),
            cancellationToken);

    private async Task<ApplicationResult<ApprovalRequest>> ResolveAsync(
        ResolveApprovalRequest request,
        Action<ApprovalRequest, string, DateTimeOffset> resolveApproval,
        Action<RuntimeTask, ApprovalRequest, DateTimeOffset> updateTask,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResolvedBy))
        {
            return ApplicationResult<ApprovalRequest>.Failure("invalid_approval_resolution", "ResolvedBy must not be empty.");
        }

        var approval = await unitOfWork.Approvals.GetByIdAsync(request.ApprovalId, cancellationToken);

        if (approval is null)
        {
            return ApplicationResult<ApprovalRequest>.Failure(
                "approval_not_found",
                $"Approval request '{request.ApprovalId}' was not found.");
        }

        if (approval.Status.IsTerminal())
        {
            return ApplicationResult<ApprovalRequest>.Failure(
                "approval_already_resolved",
                $"Approval request '{request.ApprovalId}' is already resolved.");
        }

        var task = await unitOfWork.Tasks.GetByIdAsync(approval.TaskId, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<ApprovalRequest>.Failure("task_not_found", $"Task '{approval.TaskId}' was not found.");
        }

        var now = clock.Now;

        try
        {
            updateTask(task, approval, now);
            resolveApproval(approval, request.ResolvedBy, now);
        }
        catch (InvalidTaskStatusTransitionException exception)
        {
            return ApplicationResult<ApprovalRequest>.Failure("approval_resolution_not_allowed", exception.Message);
        }
        catch (AegesDomainException exception)
        {
            return ApplicationResult<ApprovalRequest>.Failure("approval_domain_rule_violation", exception.Message);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                await unitOfWork.Approvals.UpdateAsync(approval, transactionCancellationToken);
                await unitOfWork.Tasks.UpdateAsync(task, transactionCancellationToken);
            },
            cancellationToken);

        return ApplicationResult<ApprovalRequest>.Success(approval);
    }
}
