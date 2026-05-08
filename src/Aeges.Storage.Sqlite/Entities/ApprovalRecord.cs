using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for an approval request row.
/// </summary>
internal sealed class ApprovalRecord
{
    /// <summary>
    /// Gets or sets the approval identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning task identifier.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the related iteration identifier, when approval belongs to one iteration.
    /// </summary>
    public string? IterationId { get; set; }

    /// <summary>
    /// Gets or sets the approval lifecycle status.
    /// </summary>
    public ApprovalStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the durable reason approval was required.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested action awaiting a decision.
    /// </summary>
    public string RequestedAction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when approval was requested.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when approval was resolved.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Gets or sets the actor that resolved approval.
    /// </summary>
    public string? ResolvedBy { get; set; }

    /// <summary>
    /// Gets or sets the owning task navigation.
    /// </summary>
    public TaskRecord? Task { get; set; }

    /// <summary>
    /// Gets or sets the related iteration navigation.
    /// </summary>
    public TaskIterationRecord? Iteration { get; set; }
}
