using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Defines stable callback payloads used by Telegram inline buttons.
/// </summary>
public static class TelegramCallbackData
{
    /// <summary>
    /// Gets the main menu callback payload.
    /// </summary>
    public const string MainMenu = "aeges:menu";

    /// <summary>
    /// Gets the project list callback payload.
    /// </summary>
    public const string ListProjects = "aeges:projects:list";

    /// <summary>
    /// Gets the machine list callback payload.
    /// </summary>
    public const string ListMachines = "aeges:machines:list";

    /// <summary>
    /// Gets the queued task list callback payload.
    /// </summary>
    public const string ListQueuedTasks = "aeges:tasks:queued";

    /// <summary>
    /// Gets the pending approval list callback payload.
    /// </summary>
    public const string ListPendingApprovals = "ae:ap";

    /// <summary>
    /// Creates a task details callback payload.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ViewTask(TaskId taskId) => $"aeges:task:{taskId.Value}";

    /// <summary>
    /// Creates an approval details callback payload.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ViewApproval(ApprovalId approvalId) => $"ae:a:{approvalId.Value}";

    /// <summary>
    /// Creates an approval callback payload.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ApproveApproval(ApprovalId approvalId) => $"ae:a:y:{approvalId.Value}";

    /// <summary>
    /// Creates a rejection callback payload.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string RejectApproval(ApprovalId approvalId) => $"ae:a:n:{approvalId.Value}";

    /// <summary>
    /// Attempts to parse a task details callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="taskId">The parsed task identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseViewTask(string payload, out TaskId taskId)
    {
        const string prefix = "aeges:task:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            try
            {
                taskId = new TaskId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                taskId = default;
                return false;
            }
        }

        taskId = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse an approval details callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="approvalId">The parsed approval request identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseViewApproval(string payload, out ApprovalId approvalId) =>
        TryParseApproval(payload, "ae:a:", out approvalId);

    /// <summary>
    /// Attempts to parse an approval resolution callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="approvalId">The parsed approval request identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseApproveApproval(string payload, out ApprovalId approvalId) =>
        TryParseApproval(payload, "ae:a:y:", out approvalId);

    /// <summary>
    /// Attempts to parse a rejection resolution callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="approvalId">The parsed approval request identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseRejectApproval(string payload, out ApprovalId approvalId) =>
        TryParseApproval(payload, "ae:a:n:", out approvalId);

    private static bool TryParseApproval(string payload, string prefix, out ApprovalId approvalId)
    {
        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            try
            {
                approvalId = new ApprovalId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                approvalId = default;
                return false;
            }
        }

        approvalId = default;
        return false;
    }
}
