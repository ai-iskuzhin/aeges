using Aeges.Core;

namespace Aeges.Application.Projects;

/// <summary>
/// Describes a request to register a project group.
/// </summary>
/// <param name="Name">The human-readable group name.</param>
/// <param name="Path">The optional filesystem path used for scan classification.</param>
/// <param name="GroupId">The optional group identifier. A new identifier is generated when omitted.</param>
public sealed record RegisterProjectGroupRequest(
    string Name,
    string? Path = null,
    ProjectGroupId? GroupId = null);
