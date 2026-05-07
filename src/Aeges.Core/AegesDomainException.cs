namespace Aeges.Core;

/// <summary>
/// Represents an expected domain rule violation raised by the Aeges runtime model.
/// </summary>
public class AegesDomainException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AegesDomainException"/> class.
    /// </summary>
    /// <param name="message">The domain error message.</param>
    public AegesDomainException(string message)
        : base(message)
    {
    }
}
