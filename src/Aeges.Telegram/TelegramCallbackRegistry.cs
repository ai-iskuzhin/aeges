using Aeges.Application;
using Aeges.Application.Transports;
using Aeges.Core;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aeges.Telegram;

/// <summary>
/// SQLite-backed registry that maps Telegram-safe callback tokens to logical callback payloads.
/// </summary>
public sealed class TelegramCallbackRegistry : ITelegramCallbackRegistry
{
    private const string Transport = "telegram";
    private const string ActionType = "callback";
    private const string TokenPrefix = "a:";
    private const int TokenBytes = 9;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromDays(1);
    private readonly TransportCallbackActionService callbackActions;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramCallbackRegistry"/> class.
    /// </summary>
    /// <param name="callbackActions">The application service for durable callback action state.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public TelegramCallbackRegistry(TransportCallbackActionService callbackActions, IClock clock)
    {
        this.callbackActions = callbackActions;
        this.clock = clock;
    }

    /// <inheritdoc />
    public async Task<TelegramResponse> TokenizeAsync(
        long chatId,
        TelegramResponse response,
        CancellationToken cancellationToken)
    {
        if (response.Buttons.Rows.Count == 0)
        {
            return response;
        }

        var rows = new List<IReadOnlyList<TelegramButton>>(response.Buttons.Rows.Count);

        foreach (var row in response.Buttons.Rows)
        {
            var buttons = new List<TelegramButton>(row.Count);

            foreach (var button in row)
            {
                var callbackData = IsTokenized(button.CallbackData)
                    ? button.CallbackData
                    : await RegisterAsync(chatId, button.CallbackData, cancellationToken);
                buttons.Add(button with { CallbackData = callbackData });
            }

            rows.Add(buttons);
        }

        return response with
        {
            Buttons = new TelegramButtonMarkup(rows),
        };
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(
        long chatId,
        string callbackData,
        CancellationToken cancellationToken)
    {
        if (!TryReadToken(callbackData, out var token))
        {
            return callbackData;
        }

        var action = await callbackActions.ResolveAsync(Transport, CreateScope(chatId), token, cancellationToken);

        return action is null ? null : TryReadCallbackData(action.PayloadJson);
    }

    private async Task<string> RegisterAsync(
        long chatId,
        string callbackData,
        CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var scope = CreateScope(chatId);
        var payloadJson = JsonSerializer.Serialize(new TelegramCallbackPayload(callbackData));

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var token = CreateToken();
            var action = RuntimeTransportCallbackAction.Create(
                token,
                Transport,
                scope,
                ActionType,
                payloadJson,
                now,
                now.Add(DefaultExpiration));

            if (await callbackActions.TryRegisterAsync(action, cancellationToken))
            {
                return $"{TokenPrefix}{token}";
            }
        }

        throw new InvalidOperationException("Could not allocate a unique Telegram callback token.");
    }

    private static bool IsTokenized(string callbackData) =>
        callbackData.StartsWith(TokenPrefix, StringComparison.Ordinal);

    private static bool TryReadToken(string callbackData, out string token)
    {
        if (callbackData.StartsWith(TokenPrefix, StringComparison.Ordinal)
            && callbackData.Length > TokenPrefix.Length)
        {
            token = callbackData[TokenPrefix.Length..];
            return true;
        }

        token = string.Empty;
        return false;
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[TokenBytes];
        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string CreateScope(long chatId) =>
        $"chat:{chatId.ToString(CultureInfo.InvariantCulture)}";

    private static string? TryReadCallbackData(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<TelegramCallbackPayload>(payloadJson)?.CallbackData;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record TelegramCallbackPayload(string CallbackData);
}
