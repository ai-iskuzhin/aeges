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
}
