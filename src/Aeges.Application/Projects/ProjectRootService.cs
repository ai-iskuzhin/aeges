using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Projects;

/// <summary>
/// Coordinates project discovery root registration and lookup use cases.
/// </summary>
public sealed class ProjectRootService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRootService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public ProjectRootService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Registers a project discovery root.
    /// </summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project root.</returns>
    public async Task<ApplicationResult<RuntimeProjectRoot>> RegisterAsync(
        RegisterProjectRootRequest request,
        CancellationToken cancellationToken)
    {
        var root = RuntimeProjectRoot.Create(
            request.RootId ?? ProjectRootId.New(),
            request.Name,
            request.Path,
            clock.Now);

        await unitOfWork.ProjectRoots.AddAsync(root, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeProjectRoot>.Success(root);
    }

    /// <summary>
    /// Lists registered project discovery roots.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project discovery roots.</returns>
    public async Task<IReadOnlyList<RuntimeProjectRoot>> ListAsync(CancellationToken cancellationToken) =>
        await unitOfWork.ProjectRoots.ListAsync(cancellationToken);

    /// <summary>
    /// Gets a project discovery root by identifier.
    /// </summary>
    /// <param name="rootId">The project root identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The root, or an expected failure when no root exists.</returns>
    public async Task<ApplicationResult<RuntimeProjectRoot>> GetAsync(
        ProjectRootId rootId,
        CancellationToken cancellationToken)
    {
        var root = await unitOfWork.ProjectRoots.GetByIdAsync(rootId, cancellationToken);

        return root is null
            ? ApplicationResult<RuntimeProjectRoot>.Failure("project_root_not_found", $"Project root '{rootId}' was not found.")
            : ApplicationResult<RuntimeProjectRoot>.Success(root);
    }
}
