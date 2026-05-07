namespace Aeges.Application;

/// <summary>
/// Represents the result of an application use case.
/// </summary>
/// <typeparam name="TValue">The successful result value type.</typeparam>
public sealed class ApplicationResult<TValue>
{
    private ApplicationResult(TValue? value, ApplicationError? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    /// <summary>
    /// Gets a value indicating whether the use case succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the successful result value.
    /// </summary>
    public TValue? Value { get; }

    /// <summary>
    /// Gets the expected error when the use case fails.
    /// </summary>
    public ApplicationError? Error { get; }

    /// <summary>
    /// Creates a successful application result.
    /// </summary>
    /// <param name="value">The successful result value.</param>
    /// <returns>A successful result.</returns>
    public static ApplicationResult<TValue> Success(TValue value) =>
        new(value, error: null, isSuccess: true);

    /// <summary>
    /// Creates a failed application result.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>A failed result.</returns>
    public static ApplicationResult<TValue> Failure(string code, string message) =>
        new(value: default, new ApplicationError(code, message), isSuccess: false);
}
