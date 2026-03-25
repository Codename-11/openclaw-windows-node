# Updating OpenClaw Windows Node

This fork is configured to update from GitHub Releases on the configured owner/repo feed.

## Default update source
By default, this fork checks:
- Owner: `Codename-11`
- Repo: `openclaw-windows-node`

The built-in updater reads those values from Settings.

## In-app update flow
In the tray app Settings window:
- Enable updates
- Confirm/update the GitHub owner and repo
- On startup, the app checks that release feed for a newer version
- If a newer release exists, the app offers to download and install it

## Recommended release model
Use GitHub Releases on the fork as the canonical update source.

Suggested workflow:
1. Merge or rebase upstream changes into the fork as needed
2. Add Axiom/Codename-11 fixes in the fork
3. Publish a release on `Codename-11/openclaw-windows-node`
4. Let the Windows app update from that release feed

## Manual update fallback
If you need to update manually on Windows:
1. Pull latest code from the fork
2. Build the WinUI project for your target runtime (`win-x64` or `win-arm64`)
3. Replace the installed executable/package with the new build

## Notes
- The app's updater now targets the configurable owner/repo feed instead of a hardcoded upstream project.
- Stable channel uses GitHub Releases and can install updates in-app.
- Beta/dev channel resolves a branch or exact commit SHA and opens the matching successful GitHub Actions run so you can fetch that artifact build.
- Keep assembly/package versioning accurate so the updater can detect new versions correctly.

## Bootstrap install
For normal Windows installs, use the bootstrap script from the fork:

```powershell
irm https://raw.githubusercontent.com/Codename-11/openclaw-windows-node/master/scripts/install.ps1 | iex
```

Optional launch after install:

```powershell
irm https://raw.githubusercontent.com/Codename-11/openclaw-windows-node/master/scripts/install.ps1 | iex -ArgumentList '-Launch'
```
