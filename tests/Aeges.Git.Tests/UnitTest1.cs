namespace Aeges.Git.Tests;

public class UnitTest1
{
    [Fact]
    public void ProjectLoads()
    {
        Assert.NotNull(typeof(object).Assembly);
    }
}
