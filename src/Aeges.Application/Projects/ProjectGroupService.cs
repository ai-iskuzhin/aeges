using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Projects;

/// <summary>
/// Coordinates project group registration and lookup use cases.
/// </summary>
public sealed class ProjectGroupService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectGroupService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public ProjectGroupService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Registers a project group.
    /// </summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project group.</returns>
    public async Task<ApplicationResult<RuntimeProjectGroup>> RegisterAsync(
        RegisterProjectGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = RuntimeProjectGroup.Create(
            request.GroupId ?? ProjectGroupId.New(),
            request.Name,
            clock.Now,
            request.Path);

        await unitOfWork.ProjectGroups.AddAsync(group, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeProjectGroup>.Success(group);
    }

    /// <summary>
    /// Lists registered project groups.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project groups.</returns>
    public async Task<IReadOnlyList<RuntimeProjectGroup>> ListAsync(CancellationToken cancellationToken) =>
        await unitOfWork.ProjectGroups.ListAsync(cancellationToken);

    /// <summary>
    /// Gets a project group by identifier.
    /// </summary>
    /// <param name="groupId">The project group identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The group, or an expected failure when no group exists.</returns>
    public async Task<ApplicationResult<RuntimeProjectGroup>> GetAsync(
        ProjectGroupId groupId,
        CancellationToken cancellationToken)
    {
        var group = await unitOfWork.ProjectGroups.GetByIdAsync(groupId, cancellationToken);

        return group is null
            ? ApplicationResult<RuntimeProjectGroup>.Failure("project_group_not_found", $"Project group '{groupId}' was not found.")
            : ApplicationResult<RuntimeProjectGroup>.Success(group);
    }
}
