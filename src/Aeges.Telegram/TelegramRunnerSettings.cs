namespace Aeges.Telegram;

/// <summary>
/// Describes runner settings exposed through the Telegram settings menu.
/// </summary>
/// <param name="CodexSandboxMode">The configured Codex sandbox mode.</param>
/// <param name="CodexBypassApprovalsAndSandbox">A value indicating whether Codex bypasses its approvals and sandbox.</param>
/// <param name="AgentMaxParallelTasks">The configured maximum number of project-isolated parallel agent tasks.</param>
public sealed record TelegramRunnerSettings(
    string CodexSandboxMode,
    bool CodexBypassApprovalsAndSandbox,
    int AgentMaxParallelTasks);
