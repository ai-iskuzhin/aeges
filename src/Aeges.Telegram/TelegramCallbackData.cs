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
    /// Gets the task status menu callback payload.
    /// </summary>
    public const string TaskMenu = "ae:t";

    /// <summary>
    /// Gets the pending approval list callback payload.
    /// </summary>
    public const string ListPendingApprovals = "ae:ap";

    /// <summary>
    /// Gets the settings menu callback payload.
    /// </summary>
    public const string SettingsMenu = "ae:s";

    /// <summary>
    /// Gets the Telegram user management callback payload.
    /// </summary>
    public const string UserMenu = "ae:u";

    /// <summary>
    /// Gets the pending text response cancellation callback payload.
    /// </summary>
    public const string CancelPendingTextResponse = "ae:m:x";

    /// <summary>
    /// Gets the task creation callback payload.
    /// </summary>
    public const string CreateTask = "ae:t:new";

    /// <summary>
    /// Gets the task creation cancellation callback payload.
    /// </summary>
    public const string CancelCreateTask = "ae:t:new:x";

    /// <summary>
    /// Creates a task project selection callback payload.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string SelectTaskProject(ProjectId projectId) => $"ae:t:p:{projectId.Value}";

    /// <summary>
    /// Creates a task machine selection callback payload.
    /// </summary>
    /// <param name="machineId">The machine identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string SelectTaskMachine(MachineId machineId) => $"ae:t:m:{machineId.Value}";

    /// <summary>
    /// Creates a task status list callback payload.
    /// </summary>
    /// <param name="status">The task lifecycle status.</param>
    /// <returns>The callback payload.</returns>
    public static string ListTasksByStatus(RuntimeTaskStatus status) => $"ae:t:s:{status.ToStorageValue()}";

    /// <summary>
    /// Creates a project details callback payload.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ViewProject(ProjectId projectId) => $"ae:p:{projectId.Value}";

    /// <summary>
    /// Gets the ungrouped project list callback payload.
    /// </summary>
    public const string ViewUngroupedProjects = "ae:g:-";

    /// <summary>
    /// Creates a project group details callback payload.
    /// </summary>
    /// <param name="groupId">The project group identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ViewProjectGroup(ProjectGroupId groupId) => $"ae:g:{groupId.Value}";

    /// <summary>
    /// Creates a project archive callback payload.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ArchiveProject(ProjectId projectId) => $"ae:p:x:{projectId.Value}";

    /// <summary>
    /// Creates a project archive confirmation callback payload.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ConfirmArchiveProject(ProjectId projectId) => $"ae:p:x:y:{projectId.Value}";

    /// <summary>
    /// Creates a Telegram user details callback payload.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ViewTelegramUser(TelegramUserId userId) => $"ae:u:v:{userId.Value}";

    /// <summary>
    /// Creates a Telegram user approval callback payload.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ApproveTelegramUser(TelegramUserId userId) => $"ae:u:y:{userId.Value}";

    /// <summary>
    /// Creates a Telegram user denial callback payload.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string DenyTelegramUser(TelegramUserId userId) => $"ae:u:n:{userId.Value}";

    /// <summary>
    /// Creates a Telegram project access toggle callback payload.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="allowed">A value indicating whether access should be granted.</param>
    /// <returns>The callback payload.</returns>
    public static string SetTelegramProjectAccess(TelegramUserId userId, ProjectId projectId, bool allowed) =>
        $"ae:u:p:{(allowed ? "1" : "0")}:{userId.Value}:{projectId.Value}";

    /// <summary>
    /// Creates a Telegram project group access toggle callback payload.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="projectGroupId">The project group identifier.</param>
    /// <param name="allowed">A value indicating whether access should be granted.</param>
    /// <returns>The callback payload.</returns>
    public static string SetTelegramProjectGroupAccess(
        TelegramUserId userId,
        ProjectGroupId projectGroupId,
        bool allowed) =>
        $"ae:u:g:{(allowed ? "1" : "0")}:{userId.Value}:{projectGroupId.Value}";

    /// <summary>
    /// Creates a project-scoped task status list callback payload.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="status">The task lifecycle status.</param>
    /// <returns>The callback payload.</returns>
    public static string ListProjectTasksByStatus(ProjectId projectId, RuntimeTaskStatus status) =>
        $"ae:p:{projectId.Value}:s:{ToStatusCode(status)}";

    /// <summary>
    /// Creates a task details callback payload.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ViewTask(TaskId taskId) => $"aeges:task:{taskId.Value}";

    /// <summary>
    /// Creates a task cancellation callback payload.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string CancelTask(TaskId taskId) => $"ae:t:x:{taskId.Value}";

    /// <summary>
    /// Creates a task continuation callback payload.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string ContinueTask(TaskId taskId) => $"ae:t:more:{taskId.Value}";

    /// <summary>
    /// Creates a Codex sandbox mode settings callback payload.
    /// </summary>
    /// <param name="sandboxMode">The target sandbox mode.</param>
    /// <returns>The callback payload.</returns>
    public static string SetCodexSandboxMode(string sandboxMode) => $"ae:s:sb:{sandboxMode}";

    /// <summary>
    /// Creates a Codex approvals and sandbox bypass settings callback payload.
    /// </summary>
    /// <param name="enabled">A value indicating whether bypass should be enabled.</param>
    /// <returns>The callback payload.</returns>
    public static string SetCodexBypassApprovalsAndSandbox(bool enabled) => $"ae:s:bp:{(enabled ? "1" : "0")}";

    /// <summary>
    /// Creates a task completion callback payload.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>The callback payload.</returns>
    public static string CompleteTask(TaskId taskId) => $"ae:t:done:{taskId.Value}";

    /// <summary>
    /// Gets the task continuation cancellation callback payload.
    /// </summary>
    public const string CancelContinueTask = "ae:t:more:x";

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
    /// Attempts to parse a task cancellation callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="taskId">The parsed task identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseCancelTask(string payload, out TaskId taskId)
    {
        const string prefix = "ae:t:x:";

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
    /// Attempts to parse a task continuation callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="taskId">The parsed task identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseContinueTask(string payload, out TaskId taskId)
    {
        const string prefix = "ae:t:more:";

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
    /// Attempts to parse a Codex sandbox mode settings callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="sandboxMode">The parsed sandbox mode.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseSetCodexSandboxMode(string payload, out string sandboxMode)
    {
        const string prefix = "ae:s:sb:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            sandboxMode = payload[prefix.Length..];
            return sandboxMode is "workspace-write" or "danger-full-access";
        }

        sandboxMode = string.Empty;
        return false;
    }

    /// <summary>
    /// Attempts to parse a Codex approvals and sandbox bypass settings callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="enabled">A value indicating whether bypass should be enabled.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseSetCodexBypassApprovalsAndSandbox(string payload, out bool enabled)
    {
        const string prefix = "ae:s:bp:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length == prefix.Length + 1)
        {
            if (payload[^1] == '1')
            {
                enabled = true;
                return true;
            }

            if (payload[^1] == '0')
            {
                enabled = false;
                return true;
            }
        }

        enabled = false;
        return false;
    }

    /// <summary>
    /// Attempts to parse a task completion callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="taskId">The parsed task identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseCompleteTask(string payload, out TaskId taskId)
    {
        const string prefix = "ae:t:done:";

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
    /// Attempts to parse a task project selection callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="projectId">The parsed project identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseSelectTaskProject(string payload, out ProjectId projectId)
    {
        const string prefix = "ae:t:p:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            try
            {
                projectId = new ProjectId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                projectId = default;
                return false;
            }
        }

        projectId = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a task machine selection callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="machineId">The parsed machine identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseSelectTaskMachine(string payload, out MachineId machineId)
    {
        const string prefix = "ae:t:m:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            try
            {
                machineId = new MachineId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                machineId = default;
                return false;
            }
        }

        machineId = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a task status list callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="status">The parsed task lifecycle status.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseListTasksByStatus(string payload, out RuntimeTaskStatus status)
    {
        const string prefix = "ae:t:s:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            try
            {
                status = RuntimeTaskStatusExtensions.FromStorageValue(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                status = default;
                return false;
            }
        }

        status = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a project details callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="projectId">The parsed project identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseViewProject(string payload, out ProjectId projectId)
    {
        const string prefix = "ae:p:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal)
            && payload.Length > prefix.Length
            && !payload[prefix.Length..].Contains(":s:", StringComparison.Ordinal)
            && !payload.StartsWith("ae:p:x:", StringComparison.Ordinal))
        {
            try
            {
                projectId = new ProjectId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                projectId = default;
                return false;
            }
        }

        projectId = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a project group details callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="groupId">The parsed project group identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseViewProjectGroup(string payload, out ProjectGroupId groupId)
    {
        const string prefix = "ae:g:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal)
            && payload.Length > prefix.Length
            && payload != ViewUngroupedProjects)
        {
            try
            {
                groupId = new ProjectGroupId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                groupId = default;
                return false;
            }
        }

        groupId = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a project archive callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="projectId">The parsed project identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseArchiveProject(string payload, out ProjectId projectId)
    {
        const string prefix = "ae:p:x:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal)
            && payload.Length > prefix.Length
            && !payload.StartsWith("ae:p:x:y:", StringComparison.Ordinal))
        {
            try
            {
                projectId = new ProjectId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                projectId = default;
                return false;
            }
        }

        projectId = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a project archive confirmation callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="projectId">The parsed project identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseConfirmArchiveProject(string payload, out ProjectId projectId)
    {
        const string prefix = "ae:p:x:y:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            try
            {
                projectId = new ProjectId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                projectId = default;
                return false;
            }
        }

        projectId = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a Telegram user details callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="userId">The parsed Telegram user identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseViewTelegramUser(string payload, out TelegramUserId userId) =>
        TryParseTelegramUser(payload, "ae:u:v:", out userId);

    /// <summary>
    /// Attempts to parse a Telegram user approval callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="userId">The parsed Telegram user identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseApproveTelegramUser(string payload, out TelegramUserId userId) =>
        TryParseTelegramUser(payload, "ae:u:y:", out userId);

    /// <summary>
    /// Attempts to parse a Telegram user denial callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="userId">The parsed Telegram user identifier.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseDenyTelegramUser(string payload, out TelegramUserId userId) =>
        TryParseTelegramUser(payload, "ae:u:n:", out userId);

    /// <summary>
    /// Attempts to parse a Telegram project access toggle callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="userId">The parsed Telegram user identifier.</param>
    /// <param name="projectId">The parsed project identifier.</param>
    /// <param name="allowed">A value indicating whether access should be granted.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseSetTelegramProjectAccess(
        string payload,
        out TelegramUserId userId,
        out ProjectId projectId,
        out bool allowed)
    {
        const string prefix = "ae:u:p:";
        userId = default;
        projectId = default;
        allowed = false;

        if (!payload.StartsWith(prefix, StringComparison.Ordinal) || payload.Length <= prefix.Length + 2)
        {
            return false;
        }

        allowed = payload[prefix.Length] == '1';
        var rest = payload[(prefix.Length + 2)..];
        var delimiter = rest.IndexOf(':', StringComparison.Ordinal);

        if (delimiter <= 0 || delimiter >= rest.Length - 1)
        {
            return false;
        }

        try
        {
            userId = new TelegramUserId(rest[..delimiter]);
            projectId = new ProjectId(rest[(delimiter + 1)..]);
            return true;
        }
        catch (ArgumentException)
        {
            userId = default;
            projectId = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse a Telegram project group access toggle callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="userId">The parsed Telegram user identifier.</param>
    /// <param name="projectGroupId">The parsed project group identifier.</param>
    /// <param name="allowed">A value indicating whether access should be granted.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseSetTelegramProjectGroupAccess(
        string payload,
        out TelegramUserId userId,
        out ProjectGroupId projectGroupId,
        out bool allowed)
    {
        const string prefix = "ae:u:g:";
        userId = default;
        projectGroupId = default;
        allowed = false;

        if (!payload.StartsWith(prefix, StringComparison.Ordinal) || payload.Length <= prefix.Length + 2)
        {
            return false;
        }

        allowed = payload[prefix.Length] == '1';
        var rest = payload[(prefix.Length + 2)..];
        var delimiter = rest.IndexOf(':', StringComparison.Ordinal);

        if (delimiter <= 0 || delimiter >= rest.Length - 1)
        {
            return false;
        }

        try
        {
            userId = new TelegramUserId(rest[..delimiter]);
            projectGroupId = new ProjectGroupId(rest[(delimiter + 1)..]);
            return true;
        }
        catch (ArgumentException)
        {
            userId = default;
            projectGroupId = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse a project-scoped task status list callback payload.
    /// </summary>
    /// <param name="payload">The callback payload.</param>
    /// <param name="projectId">The parsed project identifier.</param>
    /// <param name="status">The parsed task lifecycle status.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParseListProjectTasksByStatus(
        string payload,
        out ProjectId projectId,
        out RuntimeTaskStatus status)
    {
        const string prefix = "ae:p:";
        const string delimiter = ":s:";

        if (payload.StartsWith(prefix, StringComparison.Ordinal))
        {
            var delimiterIndex = payload.IndexOf(delimiter, prefix.Length, StringComparison.Ordinal);

            if (delimiterIndex > prefix.Length && delimiterIndex + delimiter.Length < payload.Length)
            {
                try
                {
                    projectId = new ProjectId(payload[prefix.Length..delimiterIndex]);
                    return TryParseStatusCode(payload[(delimiterIndex + delimiter.Length)..], out status);
                }
                catch (ArgumentException)
                {
                    projectId = default;
                    status = default;
                    return false;
                }
            }
        }

        projectId = default;
        status = default;
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

    private static bool TryParseTelegramUser(string payload, string prefix, out TelegramUserId userId)
    {
        if (payload.StartsWith(prefix, StringComparison.Ordinal) && payload.Length > prefix.Length)
        {
            try
            {
                userId = new TelegramUserId(payload[prefix.Length..]);
                return true;
            }
            catch (ArgumentException)
            {
                userId = default;
                return false;
            }
        }

        userId = default;
        return false;
    }

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

    private static string ToStatusCode(RuntimeTaskStatus status) => status switch
    {
        RuntimeTaskStatus.Queued => "q",
        RuntimeTaskStatus.Planning => "p",
        RuntimeTaskStatus.Running => "r",
        RuntimeTaskStatus.Reviewing => "v",
        RuntimeTaskStatus.WaitingApproval => "w",
        RuntimeTaskStatus.Completed => "c",
        RuntimeTaskStatus.Failed => "f",
        RuntimeTaskStatus.Cancelled => "x",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status."),
    };

    private static bool TryParseStatusCode(string code, out RuntimeTaskStatus status)
    {
        status = code switch
        {
            "q" => RuntimeTaskStatus.Queued,
            "p" => RuntimeTaskStatus.Planning,
            "r" => RuntimeTaskStatus.Running,
            "v" => RuntimeTaskStatus.Reviewing,
            "w" => RuntimeTaskStatus.WaitingApproval,
            "c" => RuntimeTaskStatus.Completed,
            "f" => RuntimeTaskStatus.Failed,
            "x" => RuntimeTaskStatus.Cancelled,
            _ => default,
        };

        return code is "q" or "p" or "r" or "v" or "w" or "c" or "f" or "x";
    }
}
