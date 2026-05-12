using Aeges.Core;

namespace Aeges.Application.Projects;

/// <summary>
/// Describes a request to register a project with the runtime.
/// </summary>
/// <param name="Name">The human-readable project name.</param>
/// <param name="Path">The project root path.</param>
/// <param name="ProjectId">The optional project identifier. A new identifier is generated when omitted.</param>
/// <param name="GroupId">The optional project group classification.</param>
public sealed record RegisterProjectRequest(
    string Name,
    string Path,
    ProjectId? ProjectId = null,
    ProjectGroupId? GroupId = null);
