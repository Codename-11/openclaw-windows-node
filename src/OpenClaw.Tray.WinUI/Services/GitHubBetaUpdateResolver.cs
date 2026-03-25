using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClawTray.Services;

internal sealed class GitHubBetaUpdateResolver
{
    private static readonly HttpClient s_httpClient = CreateHttpClient();

    public async Task<BetaUpdateInfo?> ResolveAsync(string owner, string repo, string branch, string? commitSha)
    {
        var normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "main" : branch.Trim();
        var normalizedCommit = string.IsNullOrWhiteSpace(commitSha) ? null : commitSha.Trim();

        var commit = await GetCommitAsync(owner, repo, normalizedBranch, normalizedCommit);
        if (commit == null)
            return null;

        var run = await GetWorkflowRunAsync(owner, repo, normalizedBranch, commit.CommitSha);
        if (run == null)
            return null;

        return new BetaUpdateInfo
        {
            Owner = owner,
            Repo = repo,
            Branch = run.HeadBranch ?? normalizedBranch,
            CommitSha = commit.CommitSha,
            ShortCommitSha = commit.CommitSha[..Math.Min(7, commit.CommitSha.Length)],
            CommitMessage = commit.CommitMessage,
            CommitUrl = commit.CommitUrl,
            WorkflowRunUrl = run.HtmlUrl,
            SourceDescription = normalizedCommit == null
                ? $"{owner}/{repo}@{run.HeadBranch ?? normalizedBranch}"
                : $"{owner}/{repo}@{commit.CommitSha}"
        };
    }

    private static async Task<(string CommitSha, string CommitMessage, string CommitUrl)?> GetCommitAsync(string owner, string repo, string branch, string? commitSha)
    {
        var refToResolve = commitSha ?? branch;
        var url = $"https://api.github.com/repos/{owner}/{repo}/commits/{Uri.EscapeDataString(refToResolve)}";
        using var response = await s_httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var sha = root.GetProperty("sha").GetString();
        var htmlUrl = root.GetProperty("html_url").GetString();
        var message = root.GetProperty("commit").GetProperty("message").GetString();

        if (string.IsNullOrWhiteSpace(sha) || string.IsNullOrWhiteSpace(htmlUrl))
            return null;

        return (sha, FirstLine(message, "No commit message"), htmlUrl);
    }

    private static async Task<(string HtmlUrl, string? HeadBranch)?> GetWorkflowRunAsync(string owner, string repo, string branch, string commitSha)
    {
        var exactRun = $"https://api.github.com/repos/{owner}/{repo}/actions/workflows/ci.yml/runs?head_sha={Uri.EscapeDataString(commitSha)}&status=success&per_page=20";
        var run = await FindRunAsync(exactRun, commitSha);
        if (run != null)
            return run;

        var branchRun = $"https://api.github.com/repos/{owner}/{repo}/actions/workflows/ci.yml/runs?branch={Uri.EscapeDataString(branch)}&status=success&event=push&per_page=20";
        return await FindRunAsync(branchRun, commitSha);
    }

    private static async Task<(string HtmlUrl, string? HeadBranch)?> FindRunAsync(string url, string commitSha)
    {
        using var response = await s_httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!document.RootElement.TryGetProperty("workflow_runs", out var runs))
            return null;

        foreach (var run in runs.EnumerateArray())
        {
            var conclusion = run.TryGetProperty("conclusion", out var conclusionElement)
                ? conclusionElement.GetString()
                : null;
            var headSha = run.TryGetProperty("head_sha", out var headShaElement)
                ? headShaElement.GetString()
                : null;
            var htmlUrl = run.TryGetProperty("html_url", out var htmlUrlElement)
                ? htmlUrlElement.GetString()
                : null;

            if (!string.Equals(conclusion, "success", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(htmlUrl))
                continue;

            if (!string.IsNullOrWhiteSpace(headSha) && headSha.StartsWith(commitSha, StringComparison.OrdinalIgnoreCase))
            {
                var headBranch = run.TryGetProperty("head_branch", out var headBranchElement)
                    ? headBranchElement.GetString()
                    : null;
                return (htmlUrl, headBranch);
            }
        }

        return null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenClawTray", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static string FirstLine(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim()
            ?? fallback;
    }
}
