namespace Aeges.Runners;

/// <summary>
/// Describes a short progress event emitted by a governed runner while it is executing.
/// </summary>
public sealed class RunnerProgressEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunnerProgressEvent"/> class.
    /// </summary>
    /// <param name="eventType">The stable progress event type.</param>
    /// <param name="message">The human-readable progress message.</param>
    /// <param name="payloadJson">Optional structured progress payload JSON.</param>
    public RunnerProgressEvent(string eventType, string message, string? payloadJson = null)
    {
        EventType = RequireText(eventType, nameof(eventType));
        Message = RequireText(message, nameof(message));
        PayloadJson = RequireOptionalText(payloadJson, nameof(payloadJson));
    }

    /// <summary>
    /// Gets the stable progress event type.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the human-readable progress message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets optional structured progress payload JSON.
    /// </summary>
    public string? PayloadJson { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static string? RequireOptionalText(string? value, string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty when supplied.", parameterName);
        }

        return value;
    }
}
