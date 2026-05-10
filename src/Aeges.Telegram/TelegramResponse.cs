namespace Aeges.Telegram;

using Aeges.Core;

/// <summary>
/// Describes a Telegram response message and its inline buttons.
/// </summary>
/// <param name="Text">The response text.</param>
/// <param name="Buttons">The inline button markup.</param>
/// <param name="Metadata">Optional transport metadata used for local notification tracking.</param>
public sealed record TelegramResponse(
    string Text,
    TelegramButtonMarkup Buttons,
    TelegramResponseMetadata? Metadata = null);

/// <summary>
/// Describes transport-local metadata attached to a Telegram response.
/// </summary>
/// <param name="Kind">The response kind.</param>
/// <param name="TaskId">The related task identifier, when applicable.</param>
public sealed record TelegramResponseMetadata(
    TelegramResponseKind Kind,
    TaskId? TaskId = null);

/// <summary>
/// Classifies Telegram responses for notification and message update behavior.
/// </summary>
public enum TelegramResponseKind
{
    /// <summary>
    /// The response is a general message.
    /// </summary>
    General,

    /// <summary>
    /// The response watches a task but is not the canonical task details view.
    /// </summary>
    TaskWatch,

    /// <summary>
    /// The response is the canonical task details view.
    /// </summary>
    TaskDetails,
}
