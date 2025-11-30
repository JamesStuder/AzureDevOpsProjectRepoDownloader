using System;
using System.IO;
using LibGit2Sharp;

namespace DevOps.BulkRepoDownloader.DataAccess
{
    public class RepoAccess
    {
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
                Console.WriteLine($"Cloned {cloneUrl} to {repoPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clone repository: {ex.Message}");
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
                PullOptions options = new ()
                
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "pat", Password = patToken }
                    }
                };
                Commands.Pull(repo, new Signature("Automated Pull", "email@example.com", DateTimeOffset.Now), options);
                Console.WriteLine($"Pulled latest changes for {repoPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to pull repository: {ex.Message}");
            }
        }
    }
}