using System.Reflection;
using Aeges.Storage;

namespace Aeges.Storage.Tests;

public sealed class StorageContractTests
{
    private static readonly Type[] RepositoryTypes =
    [
        typeof(ITaskRepository),
        typeof(IIterationRepository),
        typeof(IArtifactRepository),
        typeof(IApprovalRepository),
        typeof(IProjectRepository),
        typeof(IMachineRepository),
        typeof(ILockRepository),
    ];

    [Fact]
    public void Repository_methods_accept_cancellation_token()
    {
        foreach (var repositoryType in RepositoryTypes)
        {
            var methods = repositoryType.GetMethods();

            Assert.NotEmpty(methods);

            foreach (var method in methods)
            {
                Assert.Contains(
                    method.GetParameters(),
                    parameter => parameter.ParameterType == typeof(CancellationToken));
            }
        }
    }

    [Fact]
    public void Storage_contracts_do_not_expose_provider_specific_types()
    {
        var contractTypes = RepositoryTypes.Append(typeof(IUnitOfWork));

        foreach (var contractType in contractTypes)
        {
            foreach (var method in contractType.GetMethods())
            {
                AssertProviderNeutral(method.ReturnType);

                foreach (var parameter in method.GetParameters())
                {
                    AssertProviderNeutral(parameter.ParameterType);
                }
            }

            foreach (var property in contractType.GetProperties())
            {
                AssertProviderNeutral(property.PropertyType);
            }
        }
    }

    [Fact]
    public void Unit_of_work_exposes_all_repositories()
    {
        Assert.Equal(typeof(ITaskRepository), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Tasks))?.PropertyType);
        Assert.Equal(typeof(IIterationRepository), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Iterations))?.PropertyType);
        Assert.Equal(typeof(IArtifactRepository), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Artifacts))?.PropertyType);
        Assert.Equal(typeof(IApprovalRepository), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Approvals))?.PropertyType);
        Assert.Equal(typeof(IProjectRepository), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Projects))?.PropertyType);
        Assert.Equal(typeof(IMachineRepository), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Machines))?.PropertyType);
        Assert.Equal(typeof(ILockRepository), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Locks))?.PropertyType);
    }

    private static void AssertProviderNeutral(Type type)
    {
        if (type.FullName is not null)
        {
            Assert.DoesNotContain("Sqlite", type.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("EntityFramework", type.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Microsoft.Data", type.FullName, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var argument in type.GetGenericArguments())
        {
            AssertProviderNeutral(argument);
        }
    }
}
