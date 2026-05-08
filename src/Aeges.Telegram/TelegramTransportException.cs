namespace Aeges.Telegram;

/// <summary>
/// Represents an expected Telegram transport configuration or execution failure.
/// </summary>
public sealed class TelegramTransportException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramTransportException"/> class.
    /// </summary>
    /// <param name="message">The failure message.</param>
    public TelegramTransportException(string message)
        : base(message)
    {
    }
}
