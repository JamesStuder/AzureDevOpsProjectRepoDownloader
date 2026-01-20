using System;
using System.IO;
using System.Linq;
using DevOps.BulkRepoDownloader.Services;
using LibGit2Sharp;

namespace DevOps.BulkRepoDownloader.DataAccess
{
    public class RepoAccess
    {
        private readonly ConsoleService _consoleService = new();

        /// <summary>
        /// Clones a Git repository from the specified remote URL to the given local file path.
        /// </summary>
        /// <param name="cloneUrl">The URL of the remote Git repository to be cloned.</param>
        /// <param name="repoPath">The local directory path where the repository will be cloned.</param>
        /// <param name="patToken">The personal access token used for authentication with the remote repository, if required.</param>
        public void CloneRepository(string cloneUrl, string repoPath, string? patToken)
        {
            try
            {
                CloneOptions options = new ()
                {
                    FetchOptions =
                    {
                        CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "pat", Password = patToken }
                    }
                };
               
                Directory.CreateDirectory(Path.GetDirectoryName(repoPath)!);
                Repository.Clone(cloneUrl, repoPath, options);
                _consoleService.WriteSuccess($"Cloned {cloneUrl} to {repoPath}");
               
                using Repository repo = new (repoPath);
                FetchAllBranches(repo, patToken);
            }
            catch (Exception ex)
            {
                _consoleService.WriteError($"Failed to clone repository: {ex.Message}");
            }
        }

        /// <summary>
        /// Pulls the latest changes from a remote Git repository into the specified local repository.
        /// </summary>
        /// <param name="repoPath">The file path to the local repository where changes will be pulled.</param>
        /// <param name="patToken">The personal access token used for authentication with the remote repository.</param>
        public void PullRepository(string repoPath, string? patToken)
        {
            try
            {
                using Repository repo = new (repoPath);
                
                FetchAllBranches(repo, patToken);
                
                PullOptions options = new ()
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "pat", Password = patToken }
                    }
                };
                Commands.Pull(repo, new Signature("Automated Pull", "email@example.com", DateTimeOffset.Now), options);
                _consoleService.WriteSuccess($"Pulled latest changes for {repoPath}");
            }
            catch (Exception ex)
            {
                _consoleService.WriteError($"Failed to pull repository: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetches all branches from the remote repository and ensures that they are available locally.
        /// </summary>
        /// <param name="repo">The local repository instance from which branches will be fetched.</param>
        /// <param name="patToken">The personal access token used for authentication with the remote repository, if required.</param>
        private void FetchAllBranches(Repository repo, string? patToken)
        {
            Remote? remote = repo.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(repo, remote.Name, refSpecs, new FetchOptions
            {
                CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "pat", Password = patToken }
            }, null);

            foreach (Branch? remoteBranch in repo.Branches.Where(b => b.IsRemote && b.RemoteName == "origin"))
            {
                string localBranchName = remoteBranch.FriendlyName.Replace("origin/", "");
                Branch? localBranch = repo.Branches[localBranchName];

                if (localBranch == null)
                {
                    repo.CreateBranch(localBranchName, remoteBranch.Tip.Sha);
                    repo.Branches.Update(repo.Branches[localBranchName], b => b.Remote = "origin", b => b.UpstreamBranch = remoteBranch.CanonicalName);
                }
            }
        }
    }
}