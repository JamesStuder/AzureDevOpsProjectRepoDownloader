# Azure DevOps Project Repo Downloader

Bulk‑clone or pull all repositories across your Azure DevOps projects. On later runs, the tool pulls the latest changes instead of cloning again.

## What it does
- Discovers all projects you have access to via the Azure DevOps REST API
- For each project, lists all Git repositories
- Clones any missing repositories (all branches); performs `pull` on existing ones
- Persists connection details and discovered projects/repos to a config file
- Encrypts sensitive Personal Access Tokens (PATs) using Windows DPAPI (CurrentUser scope) while keeping the rest of the config as readable JSON

## Prerequisites
- Windows (DPAPI CurrentUser is used for encryption)
- .NET 8 SDK
- An Azure DevOps Personal Access Token (PAT) with at least Code (Read) access

## Build
1. Clone this repository
2. Open in Rider/VS or run from the terminal:
   - `dotnet build`

## Command Line Arguments
- `--skip-questions` or `-s`: Runs the application without interactive prompts. It will load the existing configuration and fail if required information is missing.

## Run (first time)
On the first run, there is no config, so the app will prompt you and then automatically discover projects and repos:
1. Start the app (from IDE or `dotnet run` in `DevOps.BulkRepoDownloader`)
2. Answer prompts:
   - Azure DevOps organization URL (e.g., `https://dev.azure.com/your_org`)
   - PAT
   - Root directory where repos should be downloaded
3. The app will:
   - Call the API to get all your projects
   - For each project, get all repositories
   - Create and save a config file `devops.config.json` in the user's `%AppData%\DevOps.BulkRepoDownloader` folder (PATs are encrypted)
   - Clone or pull repos under the root you specified, using the layout:
     - `<Root>/<Org Name>/<Project Name>/<Repo Name>`

## Run (next time)
The config is loaded automatically. The tool immediately processes the repositories (pull/clone as needed) without further prompts.

## Config file
The config is stored as a JSON file in `%AppData%\DevOps.BulkRepoDownloader\devops.config.json`. For security, all Personal Access Tokens (PATs) are encrypted using DPAPI before being saved.

Schema (logical structure):
```json
{
  "RepoRootLocation": "C:\\Path\\To\\Root",
  "Orgs": [
    {
      "BaseUrl": "https://dev.azure.com/your_org",
      "PAT": "ENC:DPAPI:CURUSR\n<base64-of-protected-bytes>",
      "Projects": [
        {
          "Name": "Project A",
          "Repositories": [ "Repo1", "Repo2" ]
        }
      ]
    }
  ]
}
```

Encryption format for PAT fields:
The application uses the `ENC:DPAPI:CURUSR` header followed by the base64-encoded protected bytes.
- The encryption is bound to the current Windows user and machine. Copying the config to another machine or user profile will make the PATs unreadable.

## Notes on authentication
- HTTP calls to Azure DevOps use Basic auth with the PAT
- Git clone/pull uses LibGit2Sharp with the PAT

## Troubleshooting
- 401/403 errors: Verify the PAT is valid and has the required scopes (Code: Read at minimum).
- Wrong organization URL: Use the org root, e.g., `https://dev.azure.com/<org>`.
- Access to some projects/repos is missing: Ensure your account has access to Azure DevOps.
- Moving to a different machine or user profile: The encrypted PATs cannot be read. Run the application to update the PATs or delete the config to recreate it.

## Uninstall/cleanup
- You can delete the `devops.config.json` file in `%AppData%\DevOps.BulkRepoDownloader` to force re‑prompting and rediscovery on next run. Deleting cloned repos does not affect the app.
