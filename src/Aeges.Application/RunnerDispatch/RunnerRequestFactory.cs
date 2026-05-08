using Aeges.Core;
using Aeges.Git;
using Aeges.Runners;

namespace Aeges.Application.RunnerDispatch;

/// <summary>
/// Builds deterministic runner requests from durable runtime state.
/// </summary>
public sealed class RunnerRequestFactory
{
    /// <summary>
    /// Creates a governed runner request for one task iteration.
    /// </summary>
    /// <param name="request">The runner request creation input.</param>
    /// <returns>The runner request, or an expected validation failure.</returns>
    public ApplicationResult<RunnerRequest> Create(CreateRunnerRequestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationError = Validate(request);

        if (validationError is not null)
        {
            return ApplicationResult<RunnerRequest>.Failure(validationError.Code, validationError.Message);
        }

        var worktreePath = GitWorktreePathBuilder.BuildIterationPath(
            request.RuntimeLayout.WorktreesPath,
            request.Project.Id,
            request.Task.Id,
            request.Iteration.Id);
        var artifactOutputDirectory = Path.Combine(
            request.RuntimeLayout.ArtifactsPath,
            SafePathSegment(request.Project.Id.Value),
            SafePathSegment(request.Task.Id.Value),
            SafePathSegment(request.Iteration.Id.Value));
        var promptPath = Path.Combine(artifactOutputDirectory, "prompt.md");

        try
        {
            return ApplicationResult<RunnerRequest>.Success(
                new RunnerRequest(
                    request.Task.Id,
                    request.Iteration.Id,
                    request.Project.Id,
                    request.Project.Path,
                    worktreePath,
                    promptPath,
                    artifactOutputDirectory,
                    request.Timeout,
                    request.EnvironmentVariables,
                    request.PolicyHints));
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<RunnerRequest>.Failure("invalid_runner_request", exception.Message);
        }
    }

    private static ApplicationError? Validate(CreateRunnerRequestRequest request)
    {
        if (request.Task.ProjectId != request.Project.Id)
        {
            return new ApplicationError(
                "project_mismatch",
                $"Task '{request.Task.Id}' belongs to project '{request.Task.ProjectId}', not '{request.Project.Id}'.");
        }

        if (request.Iteration.TaskId != request.Task.Id)
        {
            return new ApplicationError(
                "iteration_mismatch",
                $"Iteration '{request.Iteration.Id}' belongs to task '{request.Iteration.TaskId}', not '{request.Task.Id}'.");
        }

        return null;
    }

    private static string SafePathSegment(string value)
    {
        var safeCharacters = value.Select(character =>
            (character is '/' or '\\') || Path.GetInvalidFileNameChars().Contains(character)
                ? '_'
                : character);
        var segment = new string(safeCharacters.ToArray());

        return string.IsNullOrWhiteSpace(segment) ? "_" : segment;
    }
}
