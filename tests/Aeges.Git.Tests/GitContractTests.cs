using Aeges.Git;

namespace Aeges.Git.Tests;

public sealed class GitContractTests
{
    [Fact]
    public void Git_runtime_methods_accept_cancellation_tokens()
    {
        var methods = typeof(IGitRuntime)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(IGitRuntime))
            .ToArray();

        Assert.All(
            methods,
            method => Assert.Contains(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(CancellationToken)));
    }
}
