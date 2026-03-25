using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Updatum;

namespace OpenClawTray.Services;

internal sealed class AppUpdateService
{
    private readonly SettingsManager _settings;

    public AppUpdateService(SettingsManager settings)
    {
        _settings = settings;
    }

    public bool IsEnabled => _settings.UpdaterEnabled;

    public UpdatumManager CreateUpdater()
    {
        var owner = string.IsNullOrWhiteSpace(_settings.UpdaterGitHubOwner)
            ? SettingsManager.DefaultUpdaterGitHubOwner
            : _settings.UpdaterGitHubOwner.Trim();
        var repo = string.IsNullOrWhiteSpace(_settings.UpdaterGitHubRepo)
            ? SettingsManager.DefaultUpdaterGitHubRepo
            : _settings.UpdaterGitHubRepo.Trim();

        return new UpdatumManager(owner, repo)
        {
            FetchOnlyLatestRelease = true,
            InstallUpdateSingleFileExecutableName = Path.GetFileNameWithoutExtension(GetExecutablePath()),
        };
    }

    public string DescribeSource() => $"{_settings.UpdaterGitHubOwner}/{_settings.UpdaterGitHubRepo}";

    public string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? Assembly.GetExecutingAssembly().Location;
    }

    public string GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
    }

    public static bool HasArg(string[]? args, params string[] options)
    {
        if (args == null || args.Length == 0) return false;
        return args.Any(arg => options.Any(opt => string.Equals(arg, opt, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<bool> CheckForUpdatesAsync(Func<string, string, Task<bool>> promptInstallAsync)
    {
        if (!IsEnabled)
        {
            Logger.Info("Update checks disabled in settings");
            return true;
        }

        try
        {
            var updater = CreateUpdater();
            Logger.Info($"Checking for updates from {DescribeSource()}...");
            var updateFound = await updater.CheckForUpdatesAsync();

            if (!updateFound)
            {
                Logger.Info("No updates available");
                return true;
            }

            var release = updater.LatestRelease!;
            var changelog = updater.GetChangelog(true) ?? "No release notes available.";
            Logger.Info($"Update available from {DescribeSource()}: {release.TagName}");

            var shouldInstall = await promptInstallAsync(release.TagName, changelog);
            if (!shouldInstall)
                return true;

            var installed = await DownloadAndInstallUpdateAsync(updater);
            return !installed;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Update check failed for {DescribeSource()}: {ex.Message}");
            return true;
        }
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(UpdatumManager updater)
    {
        Dialogs.DownloadProgressDialog? progressDialog = null;
        try
        {
            progressDialog = new Dialogs.DownloadProgressDialog(updater, DescribeSource());
            progressDialog.ShowAsync();

            var downloadedAsset = await updater.DownloadUpdateAsync();
            progressDialog.Close();

            if (downloadedAsset == null || !File.Exists(downloadedAsset.FilePath))
            {
                Logger.Error("Update download failed or file missing");
                return false;
            }

            Logger.Info($"Installing update from {DescribeSource()} and restarting...");
            await updater.InstallUpdateAsync(downloadedAsset);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Update failed: {ex.Message}");
            progressDialog?.Close();
            return false;
        }
    }
}
