using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite;

/// <summary>
/// EF Core context for the local Aeges SQLite database.
/// </summary>
public sealed class AegesDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AegesDbContext"/> class.
    /// </summary>
    /// <param name="options">The EF Core context options.</param>
    public AegesDbContext(DbContextOptions<AegesDbContext> options)
        : base(options)
    {
    }

    internal DbSet<ProjectRecord> Projects => Set<ProjectRecord>();

    internal DbSet<ProjectGroupRecord> ProjectGroups => Set<ProjectGroupRecord>();

    internal DbSet<ProjectRootRecord> ProjectRoots => Set<ProjectRootRecord>();

    internal DbSet<TaskRecord> Tasks => Set<TaskRecord>();

    internal DbSet<TaskIterationRecord> TaskIterations => Set<TaskIterationRecord>();

    internal DbSet<ArtifactRecord> Artifacts => Set<ArtifactRecord>();

    internal DbSet<ApprovalRecord> Approvals => Set<ApprovalRecord>();

    internal DbSet<LockRecord> Locks => Set<LockRecord>();

    internal DbSet<RuntimeEventRecord> RuntimeEvents => Set<RuntimeEventRecord>();

    internal DbSet<RunnerExecutionRecord> RunnerExecutions => Set<RunnerExecutionRecord>();

    internal DbSet<MachineRecord> Machines => Set<MachineRecord>();

    internal DbSet<TalkSessionRecord> TalkSessions => Set<TalkSessionRecord>();

    internal DbSet<TalkMessageRecord> TalkMessages => Set<TalkMessageRecord>();

    internal DbSet<TransportCallbackActionRecord> TransportCallbackActions => Set<TransportCallbackActionRecord>();

    internal DbSet<TelegramUserRecord> TelegramUsers => Set<TelegramUserRecord>();

    internal DbSet<TelegramProjectAccessRecord> TelegramProjectAccess => Set<TelegramProjectAccessRecord>();

    internal DbSet<TelegramProjectGroupAccessRecord> TelegramProjectGroupAccess => Set<TelegramProjectGroupAccessRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AegesDbContext).Assembly);
    }
}
