using Aeges.Application;

namespace Aeges.Application.Tests;

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }
}
