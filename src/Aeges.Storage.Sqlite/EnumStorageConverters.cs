using Aeges.Core;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aeges.Storage.Sqlite;

/// <summary>
/// Converts task lifecycle statuses to and from their stable SQLite text values.
/// </summary>
internal sealed class RuntimeTaskStatusStorageConverter : ValueConverter<RuntimeTaskStatus, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeTaskStatusStorageConverter"/> class.
    /// </summary>
    public RuntimeTaskStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => RuntimeTaskStatusExtensions.FromStorageValue(value))
    {
    }
}

/// <summary>
/// Converts iteration lifecycle statuses to and from their stable SQLite text values.
/// </summary>
internal sealed class TaskIterationStatusStorageConverter : ValueConverter<TaskIterationStatus, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskIterationStatusStorageConverter"/> class.
    /// </summary>
    public TaskIterationStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => TaskIterationStatusExtensions.FromStorageValue(value))
    {
    }
}

/// <summary>
/// Converts approval statuses to and from their stable SQLite text values.
/// </summary>
internal sealed class ApprovalStatusStorageConverter : ValueConverter<ApprovalStatus, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalStatusStorageConverter"/> class.
    /// </summary>
    public ApprovalStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => ApprovalStatusExtensions.FromStorageValue(value))
    {
    }
}

/// <summary>
/// Converts artifact types to and from their stable SQLite text values.
/// </summary>
internal sealed class ArtifactTypeStorageConverter : ValueConverter<ArtifactType, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactTypeStorageConverter"/> class.
    /// </summary>
    public ArtifactTypeStorageConverter()
        : base(
            type => type.ToStorageValue(),
            value => ArtifactTypeExtensions.FromStorageValue(value))
    {
    }
}

/// <summary>
/// Converts machine statuses to and from their stable SQLite text values.
/// </summary>
internal sealed class MachineStatusStorageConverter : ValueConverter<MachineStatus, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MachineStatusStorageConverter"/> class.
    /// </summary>
    public MachineStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => MachineStatusExtensions.FromStorageValue(value))
    {
    }
}

/// <summary>
/// Converts Telegram user roles to and from their stable SQLite text values.
/// </summary>
internal sealed class TelegramUserRoleStorageConverter : ValueConverter<TelegramUserRole, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramUserRoleStorageConverter"/> class.
    /// </summary>
    public TelegramUserRoleStorageConverter()
        : base(
            role => role.ToStorageValue(),
            value => TelegramUserRoleExtensions.FromStorageValue(value))
    {
    }
}

/// <summary>
/// Converts Telegram user statuses to and from their stable SQLite text values.
/// </summary>
internal sealed class TelegramUserStatusStorageConverter : ValueConverter<TelegramUserStatus, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramUserStatusStorageConverter"/> class.
    /// </summary>
    public TelegramUserStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => TelegramUserStatusExtensions.FromStorageValue(value))
    {
    }
}
