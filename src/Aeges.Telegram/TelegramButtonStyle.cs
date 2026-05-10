namespace Aeges.Telegram;

/// <summary>
/// Describes Telegram inline button presentation style.
/// </summary>
public enum TelegramButtonStyle
{
    /// <summary>
    /// Uses the client default button style.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Uses Telegram's primary blue button style.
    /// </summary>
    Primary = 1,

    /// <summary>
    /// Uses Telegram's success green button style.
    /// </summary>
    Success = 2,

    /// <summary>
    /// Uses Telegram's danger red button style.
    /// </summary>
    Danger = 3,
}
