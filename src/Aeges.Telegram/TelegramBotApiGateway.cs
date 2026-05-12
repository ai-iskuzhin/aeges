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
            TelegramMarkdown.EscapeResponseText(response.Text),
            parseMode: ParseMode.MarkdownV2,
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
            TelegramMarkdown.EscapeResponseText(response.Text),
            parseMode: ParseMode.MarkdownV2,
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
            var user = callback.From;

            return new TelegramBotUpdate(
                update.Id,
                chatId,
                Text: null,
                callback.Data,
                callback.Id,
                callback.Message?.MessageId,
                user.Username,
                user.FirstName,
                user.LastName,
                user.Id,
                callback.Message?.MessageThreadId,
                ReplyToMessageId: null,
                IsPrivateChat: callback.Message?.Chat.Type == ChatType.Private || callback.Message is null && callback.From.Id == chatId);
        }

        if (update.Message is not null)
        {
            if (update.Message.Text is null)
            {
                return null;
            }

            var user = update.Message.From;

            return new TelegramBotUpdate(
                update.Id,
                update.Message.Chat.Id,
                update.Message.Text,
                CallbackData: null,
                CallbackQueryId: null,
                Username: user?.Username,
                FirstName: user?.FirstName,
                LastName: user?.LastName,
                SenderUserId: user?.Id,
                MessageThreadId: update.Message.MessageThreadId,
                ReplyToMessageId: update.Message.ReplyToMessage?.MessageId,
                IsPrivateChat: update.Message.Chat.Type == ChatType.Private);
        }

        return null;
    }

    private static InlineKeyboardMarkup? ToReplyMarkup(TelegramButtonMarkup markup)
    {
        if (markup.Rows.Count == 0)
        {
            return null;
        }

        var rows = markup.Rows.Select(row => row.Select(ToInlineKeyboardButton));

        return new InlineKeyboardMarkup(rows);
    }

    private static InlineKeyboardButton ToInlineKeyboardButton(TelegramButton button)
    {
        var inlineButton = InlineKeyboardButton.WithCallbackData(button.Text, button.CallbackData);
        inlineButton.Style = button.Style switch
        {
            TelegramButtonStyle.Primary => KeyboardButtonStyle.Primary,
            TelegramButtonStyle.Success => KeyboardButtonStyle.Success,
            TelegramButtonStyle.Danger => KeyboardButtonStyle.Danger,
            _ => null,
        };

        return inlineButton;
    }
}
