namespace Aeges.Core;

/// <summary>
/// Defines the actor that produced a discussion message.
/// </summary>
public enum TalkMessageRole
{
    /// <summary>
    /// The message was written by the human operator.
    /// </summary>
    User = 0,

    /// <summary>
    /// The message was produced by the coding agent runner.
    /// </summary>
    Assistant = 1,

    /// <summary>
    /// The message was produced by Aeges runtime infrastructure.
    /// </summary>
    System = 2,
}

/// <summary>
/// Provides conversion helpers for <see cref="TalkMessageRole"/>.
/// </summary>
public static class TalkMessageRoleExtensions
{
    /// <summary>
    /// Converts a message role into its stable storage representation.
    /// </summary>
    /// <param name="role">The role to convert.</param>
    /// <returns>The lowercase storage value.</returns>
    public static string ToStorageValue(this TalkMessageRole role) => role switch
    {
        TalkMessageRole.User => "user",
        TalkMessageRole.Assistant => "assistant",
        TalkMessageRole.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown talk message role."),
    };

    /// <summary>
    /// Parses a stable storage value into a message role.
    /// </summary>
    /// <param name="value">The persisted role value.</param>
    /// <returns>The matching message role.</returns>
    public static TalkMessageRole FromStorageValue(string value) => value switch
    {
        "user" => TalkMessageRole.User,
        "assistant" => TalkMessageRole.Assistant,
        "system" => TalkMessageRole.System,
        _ => throw new ArgumentException($"Unknown talk message role '{value}'.", nameof(value)),
    };
}
