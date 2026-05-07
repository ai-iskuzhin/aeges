using Aeges.Core;

namespace Aeges.Application.Machines;

/// <summary>
/// Describes a request to register a machine with the runtime.
/// </summary>
/// <param name="Name">The human-readable machine name.</param>
/// <param name="Platform">The machine platform description.</param>
/// <param name="MachineId">The optional machine identifier. A new identifier is generated when omitted.</param>
public sealed record RegisterMachineRequest(string Name, string Platform, MachineId? MachineId = null);
