using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Projects;

/// <summary>
/// Coordinates project registration and lookup use cases.
/// </summary>
public sealed class ProjectService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public ProjectService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Registers a project.
    /// </summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project.</returns>
    public async Task<ApplicationResult<RuntimeProject>> RegisterAsync(
        RegisterProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = RuntimeProject.Create(
            request.ProjectId ?? ProjectId.New(),
            request.Name,
            request.Path,
            clock.Now);

        await unitOfWork.Projects.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeProject>.Success(project);
    }

    /// <summary>
    /// Lists registered projects.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered projects.</returns>
    public async Task<IReadOnlyList<RuntimeProject>> ListAsync(CancellationToken cancellationToken) =>
        await unitOfWork.Projects.ListAsync(cancellationToken);

    /// <summary>
    /// Lists projects that can accept new tasks.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The active registered projects.</returns>
    public async Task<IReadOnlyList<RuntimeProject>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var projects = await unitOfWork.Projects.ListAsync(cancellationToken);

        return [.. projects.Where(project => !project.IsArchived)];
    }

    /// <summary>
    /// Gets a registered project by identifier.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The project, or an expected failure when no project exists.</returns>
    public async Task<ApplicationResult<RuntimeProject>> GetAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = await unitOfWork.Projects.GetByIdAsync(projectId, cancellationToken);

        return project is null
            ? ApplicationResult<RuntimeProject>.Failure("project_not_found", $"Project '{projectId}' was not found.")
            : ApplicationResult<RuntimeProject>.Success(project);
    }

    /// <summary>
    /// Archives a project while retaining its tasks, artifacts, and audit history.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The archived project, or an expected failure when no project exists.</returns>
    public async Task<ApplicationResult<RuntimeProject>> ArchiveAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = await unitOfWork.Projects.GetByIdAsync(projectId, cancellationToken);

        if (project is null)
        {
            return ApplicationResult<RuntimeProject>.Failure(
                "project_not_found",
                $"Project '{projectId}' was not found.");
        }

        project.Archive(clock.Now);
        await unitOfWork.Projects.UpdateAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeProject>.Success(project);
    }
}
