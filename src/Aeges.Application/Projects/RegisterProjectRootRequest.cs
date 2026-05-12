using Aeges.Core;

namespace Aeges.Application.Projects;

/// <summary>
/// Describes a request to register a project discovery root.
/// </summary>
/// <param name="Name">The human-readable root name.</param>
/// <param name="Path">The filesystem path to scan.</param>
/// <param name="RootId">The optional root identifier. A new identifier is generated when omitted.</param>
public sealed record RegisterProjectRootRequest(
    string Name,
    string Path,
    ProjectRootId? RootId = null);
