using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Artifacts;

/// <summary>
/// Coordinates artifact metadata registration and lookup use cases.
/// </summary>
public sealed class ArtifactService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public ArtifactService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Registers artifact metadata.
    /// </summary>
    /// <param name="request">The artifact registration request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered artifact, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeArtifact>> RegisterAsync(
        RegisterArtifactRequest request,
        CancellationToken cancellationToken)
    {
        var task = await unitOfWork.Tasks.GetByIdAsync(request.TaskId, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<RuntimeArtifact>.Failure("task_not_found", $"Task '{request.TaskId}' was not found.");
        }

        TaskIteration? iteration = null;

        if (request.IterationId is not null)
        {
            iteration = await unitOfWork.Iterations.GetByIdAsync(request.IterationId.Value, cancellationToken);

            if (iteration is null || iteration.TaskId != request.TaskId)
            {
                return ApplicationResult<RuntimeArtifact>.Failure(
                    "iteration_not_found",
                    $"Iteration '{request.IterationId}' was not found for task '{request.TaskId}'.");
            }
        }

        RuntimeArtifact artifact;

        var now = clock.Now;

        try
        {
            artifact = new RuntimeArtifact(
                request.ArtifactId ?? ArtifactId.New(),
                request.TaskId,
                request.IterationId,
                request.Type,
                request.RelativePath,
                now,
                request.SizeBytes,
                request.Sha256);

            AttachToIterationWhenSupported(iteration, artifact, now);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ApplicationResult<RuntimeArtifact>.Failure("invalid_artifact_metadata", exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<RuntimeArtifact>.Failure("invalid_artifact_metadata", exception.Message);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                await unitOfWork.Artifacts.AddAsync(artifact, transactionCancellationToken);

                if (iteration is not null && ShouldAttachToIteration(artifact.Type))
                {
                    await unitOfWork.Iterations.UpdateAsync(iteration, transactionCancellationToken);
                }
            },
            cancellationToken);

        return ApplicationResult<RuntimeArtifact>.Success(artifact);
    }

    /// <summary>
    /// Gets artifact metadata by identifier.
    /// </summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The artifact metadata, or an expected failure when it does not exist.</returns>
    public async Task<ApplicationResult<RuntimeArtifact>> GetAsync(
        ArtifactId artifactId,
        CancellationToken cancellationToken)
    {
        var artifact = await unitOfWork.Artifacts.GetByIdAsync(artifactId, cancellationToken);

        return artifact is null
            ? ApplicationResult<RuntimeArtifact>.Failure("artifact_not_found", $"Artifact '{artifactId}' was not found.")
            : ApplicationResult<RuntimeArtifact>.Success(artifact);
    }

    /// <summary>
    /// Lists artifacts belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task artifacts.</returns>
    public async Task<IReadOnlyList<RuntimeArtifact>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Artifacts.ListByTaskAsync(taskId, cancellationToken);

    /// <summary>
    /// Lists artifacts belonging to an iteration.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The iteration artifacts.</returns>
    public async Task<IReadOnlyList<RuntimeArtifact>> ListByIterationAsync(
        IterationId iterationId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Artifacts.ListByIterationAsync(iterationId, cancellationToken);

    private static void AttachToIterationWhenSupported(
        TaskIteration? iteration,
        RuntimeArtifact artifact,
        DateTimeOffset now)
    {
        if (iteration is null)
        {
            return;
        }

        switch (artifact.Type)
        {
            case ArtifactType.Prompt:
                iteration.AttachPromptArtifact(artifact.Id, now);
                break;
            case ArtifactType.Result:
                iteration.AttachResultArtifact(artifact.Id, now);
                break;
            case ArtifactType.Diff:
                iteration.AttachDiffArtifact(artifact.Id, now);
                break;
        }
    }

    private static bool ShouldAttachToIteration(ArtifactType type) =>
        type is ArtifactType.Prompt or ArtifactType.Result or ArtifactType.Diff;
}
