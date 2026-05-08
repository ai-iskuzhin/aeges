using Aeges.Application;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramApplicationFacade"/> class.
    /// </summary>
    /// <param name="projectService">The project application service.</param>
    /// <param name="machineService">The machine application service.</param>
    /// <param name="taskService">The task application service.</param>
    public TelegramApplicationFacade(
        ProjectService projectService,
        MachineService machineService,
        TaskService taskService)
    {
        this.projectService = projectService;
        this.machineService = machineService;
        this.taskService = taskService;
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
    public async Task<ApplicationResult<RuntimeTask>> GetTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await taskService.GetAsync(taskId, cancellationToken);
}
