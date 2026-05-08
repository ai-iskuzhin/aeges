using Aeges.Application;
using Aeges.Application.Iterations;
using Aeges.Application.Tasks;
using Aeges.Core;
using Aeges.Storage.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Agent;

/// <summary>
/// Runs the local Aeges agent shell against the configured SQLite runtime state.
/// </summary>
public sealed class LocalAgentRuntime
{
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalAgentRuntime"/> class.
    /// </summary>
    /// <param name="clock">The deterministic runtime clock.</param>
    public LocalAgentRuntime(IClock? clock = null)
    {
        this.clock = clock ?? new SystemClock();
    }

    /// <summary>
    /// Initializes local runtime storage, records a machine heartbeat, claims queued work, and returns a queue snapshot.
    /// </summary>
    /// <param name="options">The local agent run options.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The resulting agent heartbeat snapshot.</returns>
    public async Task<AgentRunSnapshot> RunOnceAsync(
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        Validate(options);

        await using var context = new AegesDbContext(AegesDbContextOptions.Create(options.ConnectionString));
        await SqlitePragmas.ApplyAsync(context, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        var unitOfWork = new SqliteUnitOfWork(context);
        var now = clock.Now;
        var machineId = new MachineId(options.MachineId);
        var machine = await unitOfWork.Machines.GetByIdAsync(machineId, cancellationToken);

        if (machine is null)
        {
            machine = RuntimeMachine.Create(machineId, options.MachineName, options.Platform, now);
            machine.MarkOnline(now);
            await unitOfWork.Machines.AddAsync(machine, cancellationToken);
        }
        else
        {
            machine.MarkOnline(now);
            await unitOfWork.Machines.UpdateAsync(machine, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        string? claimedTaskId = null;
        string? createdIterationId = null;

        if (options.ClaimQueuedTask)
        {
            var claim = await ClaimNextQueuedTaskAsync(unitOfWork, machineId, options, cancellationToken);
            claimedTaskId = claim.ClaimedTaskId;
            createdIterationId = claim.CreatedIterationId;
        }

        var queuedTasks = await unitOfWork.Tasks.ListByStatusAsync(
            RuntimeTaskStatus.Queued,
            options.QueuedTaskPreviewLimit,
            cancellationToken);

        return new AgentRunSnapshot(
            machine.Id.Value,
            context.Database.GetDbConnection().DataSource,
            now,
            queuedTasks.Count,
            claimedTaskId,
            createdIterationId);
    }

    private async Task<ClaimResult> ClaimNextQueuedTaskAsync(
        SqliteUnitOfWork unitOfWork,
        MachineId machineId,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        var queuedTasks = await unitOfWork.Tasks.ListByStatusAsync(
            RuntimeTaskStatus.Queued,
            options.QueuedTaskPreviewLimit,
            cancellationToken);
        var task = queuedTasks.FirstOrDefault(candidate => candidate.MachineId == machineId);

        if (task is null)
        {
            return new ClaimResult(null, null);
        }

        var taskService = new TaskService(unitOfWork, clock);
        var iterationService = new TaskIterationService(unitOfWork, clock);
        var planning = await taskService.StartPlanningAsync(task.Id, cancellationToken);

        if (!planning.IsSuccess)
        {
            throw new InvalidOperationException(planning.Error!.Message);
        }

        var iteration = await iterationService.CreateNextAsync(
            new CreateTaskIterationRequest(task.Id, new RunnerId(options.RunnerId)),
            cancellationToken);

        if (!iteration.IsSuccess)
        {
            throw new InvalidOperationException(iteration.Error!.Message);
        }

        return new ClaimResult(task.Id.Value, iteration.Value!.Id.Value);
    }

    private static void Validate(AgentRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("Connection string must not be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.MachineId))
        {
            throw new ArgumentException("Machine ID must not be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.MachineName))
        {
            throw new ArgumentException("Machine name must not be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Platform))
        {
            throw new ArgumentException("Platform must not be empty.", nameof(options));
        }

        if (options.QueuedTaskPreviewLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.QueuedTaskPreviewLimit,
                "Queued task preview limit must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.RunnerId))
        {
            throw new ArgumentException("Runner ID must not be empty.", nameof(options));
        }
    }

    private sealed record ClaimResult(string? ClaimedTaskId, string? CreatedIterationId);
}
