# Azure DevOps Project Repo Downloader

Bulk‑clone or pull all repositories across your Azure DevOps projects. On subsequent runs, the tool pulls the latest changes instead of cloning again.

## What it does
- Discovers all projects you have access to via the Azure DevOps REST API
- For each project, lists all Git repositories
- Clones any missing repositories; performs `pull` on existing ones
- Persists connection details and discovered projects/repos to a config file
- Encrypts the config at rest using Windows DPAPI (CurrentUser scope)

## Prerequisites
- Windows (DPAPI CurrentUser is used for encryption)
- .NET 8 SDK
- An Azure DevOps Personal Access Token (PAT) with at least Code (Read) access

## Build
1. Clone this repository
2. Open in Rider/VS or run from terminal:
   - `dotnet build`

## Run (first time)
On first run, there is no config, so the app will prompt you and then automatically discover projects and repos:
1. Start the app (from IDE or `dotnet run` in `DevOps.BulkRepoDownloader`)
2. Answer prompts:
   - Azure DevOps organization URL (e.g., `https://dev.azure.com/your_org`)
   - PAT
   - Root directory where repos should be downloaded
3. The app will:
   - Call the API to get all your projects
   - For each project, get all repositories
   - Create and save an encrypted config file `devops.config.json` in the app folder
   - Clone or pull repos under the root you specified, using the layout:
     - `<Root>/<Project Name>/<Repo Name>`

## Run (next time)
The config is loaded automatically (and is already encrypted). The tool immediately processes the repositories (pull/clone as needed) without further prompts.

## Config file
The config is stored as an encrypted text file with a header and base64 payload. It is automatically created/updated by the app.

Schema (logical structure):
```
{
  "BaseUrl": "https://dev.azure.com/your_org",
  "PAT": "<your PAT>",
  "RepoRootLocation": "C:\\Path\\To\\Root",
  "Projects": [
    {
      "Name": "Project A",
      "Repositories": [ "Repo1", "Repo2" ]
    }
  ]
}
```

Encryption format on disk:
```
ENC:DPAPI:CURUSR
<base64-of-protected-bytes>
```
- The file is bound to the current Windows user and machine. Copying it elsewhere renders it unreadable.
- If you previously had a plaintext config, the app will read it and automatically re‑save it in encrypted form.

## Notes on authentication
- HTTP calls to Azure DevOps use Basic auth with the PAT
- Git clone/pull uses LibGit2Sharp with the PAT

## Troubleshooting
- 401/403 errors: Verify the PAT is valid and has the required scopes (Code: Read at minimum).
- Wrong organization URL: Use the org root, e.g., `https://dev.azure.com/<org>`.
- Access to some projects/repos is missing: Ensure your account has access in Azure DevOps.
- Moving to a different machine or user profile: The encrypted config cannot be read. Run once on the new machine to recreate it.

## Uninstall/cleanup
- You can delete the `devops.config.json` file to force re‑prompting and rediscovery on next run. Deleting cloned repos does not affect the app.
