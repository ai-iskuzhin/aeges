namespace Aeges.Core;

/// <summary>
/// Defines the durable runtime availability states for a registered machine.
/// </summary>
public enum MachineStatus
{
    /// <summary>
    /// The machine is known but not currently available for work.
    /// </summary>
    Offline = 0,

    /// <summary>
    /// The machine is available to receive work.
    /// </summary>
    Online = 1,

    /// <summary>
    /// The machine is currently executing work.
    /// </summary>
    Busy = 2,
}

/// <summary>
/// Provides conversion helpers for <see cref="MachineStatus"/>.
/// </summary>
public static class MachineStatusExtensions
{
    /// <summary>
    /// Converts a machine status into its stable storage representation.
    /// </summary>
    /// <param name="status">The status to convert.</param>
    /// <returns>The lowercase storage value for the machine status.</returns>
    public static string ToStorageValue(this MachineStatus status) => status switch
    {
        MachineStatus.Offline => "offline",
        MachineStatus.Online => "online",
        MachineStatus.Busy => "busy",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown machine status."),
    };

    /// <summary>
    /// Parses a stable storage value into a machine status.
    /// </summary>
    /// <param name="value">The persisted machine status value.</param>
    /// <returns>The matching machine status.</returns>
    public static MachineStatus FromStorageValue(string value) => value switch
    {
        "offline" => MachineStatus.Offline,
        "online" => MachineStatus.Online,
        "busy" => MachineStatus.Busy,
        _ => throw new ArgumentException($"Unknown machine status '{value}'.", nameof(value)),
    };
}
