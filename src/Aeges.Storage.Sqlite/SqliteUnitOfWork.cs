using Aeges.Storage.Sqlite.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite;

/// <summary>
/// EF Core SQLite implementation of <see cref="IUnitOfWork"/>.
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteUnitOfWork"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteUnitOfWork(AegesDbContext context)
    {
        this.context = context;
        Tasks = new SqliteTaskRepository(context);
        Iterations = new SqliteIterationRepository(context);
        Artifacts = new SqliteArtifactRepository(context);
        Approvals = new SqliteApprovalRepository(context);
        Projects = new SqliteProjectRepository(context);
        Machines = new SqliteMachineRepository(context);
        Locks = new SqliteLockRepository(context);
        RunnerExecutions = new SqliteRunnerExecutionRepository(context);
        TalkSessions = new SqliteTalkSessionRepository(context);
        TalkMessages = new SqliteTalkMessageRepository(context);
        TransportCallbackActions = new SqliteTransportCallbackActionRepository(context);
    }

    /// <inheritdoc />
    public ITaskRepository Tasks { get; }

    /// <inheritdoc />
    public IIterationRepository Iterations { get; }

    /// <inheritdoc />
    public IArtifactRepository Artifacts { get; }

    /// <inheritdoc />
    public IApprovalRepository Approvals { get; }

    /// <inheritdoc />
    public IProjectRepository Projects { get; }

    /// <inheritdoc />
    public IMachineRepository Machines { get; }

    /// <inheritdoc />
    public ILockRepository Locks { get; }

    /// <inheritdoc />
    public IRunnerExecutionRepository RunnerExecutions { get; }

    /// <inheritdoc />
    public ITalkSessionRepository TalkSessions { get; }

    /// <inheritdoc />
    public ITalkMessageRepository TalkMessages { get; }

    /// <inheritdoc />
    public ITransportCallbackActionRepository TransportCallbackActions { get; }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteInTransactionAsync(
            async token =>
            {
                await operation(token);
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
