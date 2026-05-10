using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Aeges.Telegram;

/// <summary>
/// Telegram.Bot based gateway for long polling, callback acknowledgement, and inline button responses.
/// </summary>
public sealed class TelegramBotApiGateway : ITelegramBotGateway
{
    private static readonly UpdateType[] AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery];
    private readonly ITelegramBotClient botClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramBotApiGateway"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    public TelegramBotApiGateway(ITelegramBotClient botClient)
    {
        this.botClient = botClient;
    }

    /// <inheritdoc />
    public async Task<TelegramBotIdentity> GetIdentityAsync(CancellationToken cancellationToken)
    {
        var user = await botClient.GetMe(cancellationToken);

        return new TelegramBotIdentity(
            user.Id,
            user.Username,
            user.FirstName,
            user.IsBot);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TelegramBotUpdate>> GetUpdatesAsync(
        int? offset,
        int limit,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var updates = await botClient.GetUpdates(
            offset,
            limit,
            timeoutSeconds,
            AllowedUpdates,
            cancellationToken);

        return updates
            .Select(MapUpdate)
            .Where(update => update is not null)
            .Cast<TelegramBotUpdate>()
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<int?> SendResponseAsync(
        long chatId,
        TelegramResponse response,
        CancellationToken cancellationToken)
    {
        var message = await botClient.SendMessage(
            chatId,
            response.Text,
            replyMarkup: ToReplyMarkup(response.Buttons),
            cancellationToken: cancellationToken);

        return message.MessageId;
    }

    /// <inheritdoc />
    public async Task EditResponseAsync(
        long chatId,
        int messageId,
        TelegramResponse response,
        CancellationToken cancellationToken)
    {
        await botClient.EditMessageText(
            chatId,
            messageId,
            response.Text,
            replyMarkup: ToReplyMarkup(response.Buttons),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task AnswerCallbackQueryAsync(
        string callbackQueryId,
        CancellationToken cancellationToken)
    {
        await botClient.AnswerCallbackQuery(callbackQueryId, cancellationToken: cancellationToken);
    }

    private static TelegramBotUpdate? MapUpdate(global::Telegram.Bot.Types.Update update)
    {
        if (update.CallbackQuery is not null)
        {
            var callback = update.CallbackQuery;
            var chatId = callback.Message?.Chat.Id ?? callback.From.Id;

            return new TelegramBotUpdate(
                update.Id,
                chatId,
                Text: null,
                callback.Data,
                callback.Id,
                callback.Message?.MessageId);
        }

        if (update.Message is not null)
        {
            return new TelegramBotUpdate(
                update.Id,
                update.Message.Chat.Id,
                update.Message.Text,
                CallbackData: null,
                CallbackQueryId: null);
        }

        return null;
    }

    private static InlineKeyboardMarkup? ToReplyMarkup(TelegramButtonMarkup markup)
    {
        if (markup.Rows.Count == 0)
        {
            return null;
        }

        var rows = markup.Rows.Select(row =>
            row.Select(button => InlineKeyboardButton.WithCallbackData(button.Text, button.CallbackData)));

        return new InlineKeyboardMarkup(rows);
    }
}
