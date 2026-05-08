using Aeges.Application;
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
    /// Initializes local runtime storage, records a machine heartbeat, and returns a queue snapshot.
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

        var queuedTasks = await unitOfWork.Tasks.ListByStatusAsync(
            RuntimeTaskStatus.Queued,
            options.QueuedTaskPreviewLimit,
            cancellationToken);

        return new AgentRunSnapshot(
            machine.Id.Value,
            context.Database.GetDbConnection().DataSource,
            now,
            queuedTasks.Count);
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
    }
}
