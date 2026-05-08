namespace Aeges.Telegram;

/// <summary>
/// Represents inline button rows returned by the Telegram transport.
/// </summary>
/// <param name="Rows">The button rows.</param>
public sealed record TelegramButtonMarkup(IReadOnlyList<IReadOnlyList<TelegramButton>> Rows)
{
    /// <summary>
    /// Gets an empty button markup.
    /// </summary>
    public static TelegramButtonMarkup Empty { get; } = new([]);
}
