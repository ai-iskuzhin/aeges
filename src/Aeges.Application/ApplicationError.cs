namespace Aeges.Application;

/// <summary>
/// Represents an expected application-layer failure.
/// </summary>
/// <param name="Code">The stable error code.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record ApplicationError(string Code, string Message);
