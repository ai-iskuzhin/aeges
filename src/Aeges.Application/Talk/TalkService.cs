using Aeges.Application.Runtime;
using Aeges.Core;
using Aeges.Runners;
using Aeges.Storage;
using System.Text;

namespace Aeges.Application.Talk;

/// <summary>
/// Coordinates durable governed discussion with a coding-agent runner.
/// </summary>
public sealed class TalkService
{
    private const int PromptHistoryLimit = 20;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;
    private readonly ITalkRunner runner;
    private readonly RuntimeDirectoryLayout runtimeLayout;
    private readonly TimeSpan timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="TalkService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    /// <param name="runner">The discussion runner.</param>
    /// <param name="runtimeLayout">The runtime directory layout.</param>
    /// <param name="timeout">The maximum runner turn timeout.</param>
    public TalkService(
        IUnitOfWork unitOfWork,
        IClock clock,
        ITalkRunner runner,
        RuntimeDirectoryLayout runtimeLayout,
        TimeSpan timeout)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
        this.runner = runner;
        this.runtimeLayout = runtimeLayout;
        this.timeout = timeout;
    }

    /// <summary>
    /// Sends one operator message and persists the runner response.
    /// </summary>
    /// <param name="request">The talk message request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The completed exchange, or an expected failure.</returns>
    public async Task<ApplicationResult<TalkExchange>> SendAsync(
        SendTalkMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Source))
        {
            return ApplicationResult<TalkExchange>.Failure("invalid_talk_source", "Talk source must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return ApplicationResult<TalkExchange>.Failure("empty_talk_message", "Talk message must not be empty.");
        }

        var now = clock.Now;
        var session = await ResolveSessionAsync(request, now, cancellationToken);

        if (!session.IsSuccess)
        {
            return ApplicationResult<TalkExchange>.Failure(session.Error!.Code, session.Error.Message);
        }

        var userMessage = RuntimeTalkMessage.Create(
            TalkMessageId.New(),
            session.Value!.Id,
            TalkMessageRole.User,
            request.Message,
            now);
        session.Value.Touch(now);

        await unitOfWork.TalkMessages.AddAsync(userMessage, cancellationToken);
        await unitOfWork.TalkSessions.UpdateAsync(session.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var messages = await unitOfWork.TalkMessages.ListBySessionAsync(session.Value.Id, cancellationToken);
        var artifactDirectory = CreateArtifactDirectory(session.Value.Id, userMessage.Id);
        var prompt = BuildPrompt(session.Value, messages);
        var runnerResult = await runner.SendAsync(
            new TalkRunnerRequest(
                session.Value.Id,
                prompt,
                runtimeLayout.RootPath,
                artifactDirectory,
                timeout,
                policyHints: new Dictionary<string, string>
                {
                    ["mode"] = "discussion",
                    ["mutations"] = "disallowed",
                },
                sessionPolicy: session.Value.ExternalSessionId is null
                    ? RunnerSessionPolicy.NewSession
                    : RunnerSessionPolicy.ResumeSession,
                externalSessionId: session.Value.ExternalSessionId),
            cancellationToken);

        if (runnerResult.ExternalSessionId is not null)
        {
            session.Value.RecordExternalSessionId(runnerResult.ExternalSessionId, clock.Now);
        }

        if (runnerResult.Status != RunnerStatus.Succeeded || string.IsNullOrWhiteSpace(runnerResult.ResponseText))
        {
            await unitOfWork.TalkSessions.UpdateAsync(session.Value, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ApplicationResult<TalkExchange>.Failure(
                "talk_runner_failed",
                runnerResult.ErrorSummary ?? "Talk runner did not produce a response.");
        }

        var assistantMessage = RuntimeTalkMessage.Create(
            TalkMessageId.New(),
            session.Value.Id,
            TalkMessageRole.Assistant,
            runnerResult.ResponseText,
            clock.Now);
        session.Value.Touch(clock.Now);

        await unitOfWork.TalkMessages.AddAsync(assistantMessage, cancellationToken);
        await unitOfWork.TalkSessions.UpdateAsync(session.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<TalkExchange>.Success(new TalkExchange(session.Value, userMessage, assistantMessage));
    }

    private async Task<ApplicationResult<RuntimeTalkSession>> ResolveSessionAsync(
        SendTalkMessageRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (request.SessionId is not null)
        {
            var explicitSession = await unitOfWork.TalkSessions.GetByIdAsync(request.SessionId.Value, cancellationToken);

            return explicitSession is null
                ? ApplicationResult<RuntimeTalkSession>.Failure(
                    "talk_session_not_found",
                    $"Talk session '{request.SessionId}' was not found.")
                : ApplicationResult<RuntimeTalkSession>.Success(explicitSession);
        }

        if (!request.StartNewSession)
        {
            var existing = await unitOfWork.TalkSessions.GetLatestOpenBySourceAsync(request.Source, cancellationToken);

            if (existing is not null)
            {
                return ApplicationResult<RuntimeTalkSession>.Success(existing);
            }
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? CreateTitle(request.Message)
            : request.Title;
        var session = RuntimeTalkSession.Create(TalkSessionId.New(), request.Source, title!, runner.Id, now);
        await unitOfWork.TalkSessions.AddAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeTalkSession>.Success(session);
    }

    private string CreateArtifactDirectory(TalkSessionId sessionId, TalkMessageId messageId)
    {
        var path = Path.Combine(runtimeLayout.ArtifactsPath, "talk", sessionId.Value, messageId.Value);
        Directory.CreateDirectory(path);

        return path;
    }

    private static string BuildPrompt(RuntimeTalkSession session, IReadOnlyList<RuntimeTalkMessage> messages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are a coding agent worker inside Aeges, a reliable governed coding runtime.");
        builder.AppendLine("This is a discussion session, not a task execution.");
        builder.AppendLine("Do not modify files, run destructive commands, change dependencies, or perform deployments in talk mode.");
        builder.AppendLine("Use the conversation to clarify ideas, diagnose at a high level, and propose governed next steps.");
        builder.AppendLine("If real work is needed, recommend creating an Aeges task.");
        builder.AppendLine();
        builder.AppendLine($"Talk session: {session.Id}");
        builder.AppendLine($"Source: {session.Source}");
        builder.AppendLine();
        builder.AppendLine("Recent conversation:");

        foreach (var message in messages.TakeLast(PromptHistoryLimit))
        {
            builder.AppendLine($"[{message.Role.ToStorageValue()}]");
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string CreateTitle(string message)
    {
        var trimmed = message.Trim();

        return trimmed.Length <= 60 ? trimmed : trimmed[..60];
    }
}
