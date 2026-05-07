using Aeges.Core;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aeges.Storage.Sqlite;

internal sealed class RuntimeTaskStatusStorageConverter : ValueConverter<RuntimeTaskStatus, string>
{
    public RuntimeTaskStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => RuntimeTaskStatusExtensions.FromStorageValue(value))
    {
    }
}

internal sealed class TaskIterationStatusStorageConverter : ValueConverter<TaskIterationStatus, string>
{
    public TaskIterationStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => TaskIterationStatusExtensions.FromStorageValue(value))
    {
    }
}

internal sealed class ApprovalStatusStorageConverter : ValueConverter<ApprovalStatus, string>
{
    public ApprovalStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => ApprovalStatusExtensions.FromStorageValue(value))
    {
    }
}

internal sealed class ArtifactTypeStorageConverter : ValueConverter<ArtifactType, string>
{
    public ArtifactTypeStorageConverter()
        : base(
            type => type.ToStorageValue(),
            value => ArtifactTypeExtensions.FromStorageValue(value))
    {
    }
}

internal sealed class MachineStatusStorageConverter : ValueConverter<MachineStatus, string>
{
    public MachineStatusStorageConverter()
        : base(
            status => status.ToStorageValue(),
            value => MachineStatusExtensions.FromStorageValue(value))
    {
    }
}
