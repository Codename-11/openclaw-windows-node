namespace OpenClawTray.Services;

internal sealed class BetaUpdateInfo
{
    public required string Owner { get; init; }
    public required string Repo { get; init; }
    public required string Branch { get; init; }
    public required string CommitSha { get; init; }
    public required string ShortCommitSha { get; init; }
    public required string CommitMessage { get; init; }
    public required string CommitUrl { get; init; }
    public required string WorkflowRunUrl { get; init; }
    public required string SourceDescription { get; init; }
}
