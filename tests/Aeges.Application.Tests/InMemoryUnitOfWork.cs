using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Tests;

internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public InMemoryUnitOfWork()
    {
        Tasks = new TaskRepository();
        Iterations = new IterationRepository();
        Artifacts = new ArtifactRepository();
        Approvals = new ApprovalRepository();
        Projects = new ProjectRepository();
        Machines = new MachineRepository();
        Locks = new LockRepository();
    }

    public ITaskRepository Tasks { get; }

    public IIterationRepository Iterations { get; }

    public IArtifactRepository Artifacts { get; }

    public IApprovalRepository Approvals { get; }

    public IProjectRepository Projects { get; }

    public IMachineRepository Machines { get; }

    public ILockRepository Locks { get; }

    public int SaveChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCount++;

        return Task.FromResult(1);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await operation(cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var result = await operation(cancellationToken);
        await SaveChangesAsync(cancellationToken);

        return result;
    }

    private sealed class ProjectRepository : IProjectRepository
    {
        private readonly Dictionary<ProjectId, RuntimeProject> projects = [];

        public Task AddAsync(RuntimeProject project, CancellationToken cancellationToken)
        {
            projects.Add(project.Id, project);

            return Task.CompletedTask;
        }

        public Task<RuntimeProject?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken) =>
            Task.FromResult(projects.GetValueOrDefault(id));

        public Task<IReadOnlyList<RuntimeProject>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeProject>>(projects.Values.OrderBy(project => project.Name).ToArray());

        public Task UpdateAsync(RuntimeProject project, CancellationToken cancellationToken)
        {
            projects[project.Id] = project;

            return Task.CompletedTask;
        }
    }

    private sealed class MachineRepository : IMachineRepository
    {
        private readonly Dictionary<MachineId, RuntimeMachine> machines = [];

        public Task AddAsync(RuntimeMachine machine, CancellationToken cancellationToken)
        {
            machines.Add(machine.Id, machine);

            return Task.CompletedTask;
        }

        public Task<RuntimeMachine?> GetByIdAsync(MachineId id, CancellationToken cancellationToken) =>
            Task.FromResult(machines.GetValueOrDefault(id));

        public Task<IReadOnlyList<RuntimeMachine>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeMachine>>(machines.Values.OrderBy(machine => machine.Name).ToArray());

        public Task UpdateAsync(RuntimeMachine machine, CancellationToken cancellationToken)
        {
            machines[machine.Id] = machine;

            return Task.CompletedTask;
        }
    }

    private sealed class TaskRepository : ITaskRepository
    {
        private readonly Dictionary<TaskId, RuntimeTask> tasks = [];

        public Task AddAsync(RuntimeTask task, CancellationToken cancellationToken)
        {
            tasks.Add(task.Id, task);

            return Task.CompletedTask;
        }

        public Task<RuntimeTask?> GetByIdAsync(TaskId id, CancellationToken cancellationToken) =>
            Task.FromResult(tasks.GetValueOrDefault(id));

        public Task<IReadOnlyList<RuntimeTask>> ListByProjectAsync(ProjectId projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTask>>(
                tasks.Values
                    .Where(task => task.ProjectId == projectId)
                    .OrderByDescending(task => task.Priority)
                    .ThenBy(task => task.CreatedAt)
                    .ToArray());

        public Task<IReadOnlyList<RuntimeTask>> ListByStatusAsync(
            RuntimeTaskStatus status,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTask>>(
                tasks.Values
                    .Where(task => task.Status == status)
                    .OrderByDescending(task => task.Priority)
                    .ThenBy(task => task.CreatedAt)
                    .Take(limit)
                    .ToArray());

        public Task UpdateAsync(RuntimeTask task, CancellationToken cancellationToken)
        {
            tasks[task.Id] = task;

            return Task.CompletedTask;
        }
    }

    private sealed class IterationRepository : IIterationRepository
    {
        private readonly Dictionary<IterationId, TaskIteration> iterations = [];

        public Task AddAsync(TaskIteration iteration, CancellationToken cancellationToken)
        {
            iterations.Add(iteration.Id, iteration);

            return Task.CompletedTask;
        }

        public Task<TaskIteration?> GetByIdAsync(IterationId id, CancellationToken cancellationToken) =>
            Task.FromResult(iterations.GetValueOrDefault(id));

        public Task<IReadOnlyList<TaskIteration>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TaskIteration>>(
                iterations.Values
                    .Where(iteration => iteration.TaskId == taskId)
                    .OrderBy(iteration => iteration.IterationNumber)
                    .ToArray());

        public Task UpdateAsync(TaskIteration iteration, CancellationToken cancellationToken)
        {
            iterations[iteration.Id] = iteration;

            return Task.CompletedTask;
        }
    }

    private sealed class ArtifactRepository : IArtifactRepository
    {
        private readonly Dictionary<ArtifactId, RuntimeArtifact> artifacts = [];

        public Task AddAsync(RuntimeArtifact artifact, CancellationToken cancellationToken)
        {
            artifacts.Add(artifact.Id, artifact);

            return Task.CompletedTask;
        }

        public Task<RuntimeArtifact?> GetByIdAsync(ArtifactId id, CancellationToken cancellationToken) =>
            Task.FromResult(artifacts.GetValueOrDefault(id));

        public Task<IReadOnlyList<RuntimeArtifact>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeArtifact>>(
                artifacts.Values
                    .Where(artifact => artifact.TaskId == taskId)
                    .OrderBy(artifact => artifact.CreatedAt)
                    .ToArray());

        public Task<IReadOnlyList<RuntimeArtifact>> ListByIterationAsync(
            IterationId iterationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeArtifact>>(
                artifacts.Values
                    .Where(artifact => artifact.IterationId == iterationId)
                    .OrderBy(artifact => artifact.CreatedAt)
                    .ToArray());
    }

    private sealed class ApprovalRepository : IApprovalRepository
    {
        private readonly Dictionary<ApprovalId, ApprovalRequest> approvals = [];

        public Task AddAsync(ApprovalRequest approval, CancellationToken cancellationToken)
        {
            approvals.Add(approval.Id, approval);

            return Task.CompletedTask;
        }

        public Task<ApprovalRequest?> GetByIdAsync(ApprovalId id, CancellationToken cancellationToken) =>
            Task.FromResult(approvals.GetValueOrDefault(id));

        public Task<IReadOnlyList<ApprovalRequest>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovalRequest>>(
                approvals.Values
                    .Where(approval => approval.TaskId == taskId)
                    .OrderBy(approval => approval.CreatedAt)
                    .ToArray());

        public Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovalRequest>>(
                approvals.Values
                    .Where(approval => approval.Status == ApprovalStatus.Pending)
                    .OrderBy(approval => approval.CreatedAt)
                    .Take(limit)
                    .ToArray());

        public Task UpdateAsync(ApprovalRequest approval, CancellationToken cancellationToken)
        {
            approvals[approval.Id] = approval;

            return Task.CompletedTask;
        }
    }

    private sealed class LockRepository : ILockRepository
    {
        public Task AddAsync(RuntimeLock runtimeLock, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RuntimeLock?> GetByIdAsync(LockId id, CancellationToken cancellationToken) =>
            Task.FromResult<RuntimeLock?>(null);

        public Task<IReadOnlyList<RuntimeLock>> ListActiveByProjectAsync(ProjectId projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeLock>>([]);

        public Task<IReadOnlyList<RuntimeLock>> ListActiveByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeLock>>([]);

        public Task UpdateAsync(RuntimeLock runtimeLock, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
