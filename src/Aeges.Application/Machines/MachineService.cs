using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Machines;

/// <summary>
/// Coordinates machine registration and heartbeat use cases.
/// </summary>
public sealed class MachineService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public MachineService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Registers a machine.
    /// </summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered machine.</returns>
    public async Task<ApplicationResult<RuntimeMachine>> RegisterAsync(
        RegisterMachineRequest request,
        CancellationToken cancellationToken)
    {
        var machine = RuntimeMachine.Create(
            request.MachineId ?? MachineId.New(),
            request.Name,
            request.Platform,
            clock.Now);

        await unitOfWork.Machines.AddAsync(machine, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeMachine>.Success(machine);
    }

    /// <summary>
    /// Records a machine heartbeat and marks the machine online.
    /// </summary>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated machine, or an expected failure when the machine does not exist.</returns>
    public async Task<ApplicationResult<RuntimeMachine>> HeartbeatAsync(
        MachineId machineId,
        CancellationToken cancellationToken)
    {
        var machine = await unitOfWork.Machines.GetByIdAsync(machineId, cancellationToken);

        if (machine is null)
        {
            return ApplicationResult<RuntimeMachine>.Failure(
                "machine_not_found",
                $"Machine '{machineId}' was not found.");
        }

        machine.MarkOnline(clock.Now);
        await unitOfWork.Machines.UpdateAsync(machine, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeMachine>.Success(machine);
    }
}
