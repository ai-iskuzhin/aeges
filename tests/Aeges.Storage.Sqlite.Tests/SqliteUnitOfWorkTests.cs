using Aeges.Core;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteUnitOfWorkTests
{
    [Fact]
    public async Task ExecuteInTransaction_commits_when_operation_succeeds()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var unitOfWork = new SqliteUnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(
            async cancellationToken =>
            {
                await unitOfWork.Projects.AddAsync(
                    RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", SqliteRepositorySeed.CreatedAt),
                    cancellationToken);
            },
            CancellationToken.None);

        var stored = await unitOfWork.Projects.GetByIdAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.NotNull(stored);
    }

    [Fact]
    public async Task ExecuteInTransaction_rolls_back_when_operation_fails()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var unitOfWork = new SqliteUnitOfWork(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                async cancellationToken =>
                {
                    await unitOfWork.Projects.AddAsync(
                        RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", SqliteRepositorySeed.CreatedAt),
                        cancellationToken);

                    throw new InvalidOperationException("Stop transaction.");
                },
                CancellationToken.None));

        var stored = await unitOfWork.Projects.GetByIdAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.Null(stored);
    }

    [Fact]
    public async Task ExecuteInTransaction_returns_result()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var unitOfWork = new SqliteUnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            cancellationToken => Task.FromResult($"token-cancellable-{cancellationToken.CanBeCanceled}"),
            CancellationToken.None);

        Assert.Equal("token-cancellable-False", result);
    }
}
