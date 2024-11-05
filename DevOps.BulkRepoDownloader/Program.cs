using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace DevOps.BulkRepoDownloader
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            // Prompt the user for required information
            Console.Write("Enter your Azure DevOps organization URL (e.g., https://dev.azure.com/your_organization): ");
            string? azureDevOpsUrl = Console.ReadLine();
            
            Console.Write("Enter your project name: ");
            string? projectName = Console.ReadLine();
            
            Console.Write("Enter your Personal Access Token (PAT): ");
            string? patToken = Console.ReadLine();

            Console.Write("Enter the directory where you want to clone the repositories: ");
            string? cloneDirectory = Console.ReadLine();

            // Retrieve repository names and clone or pull each repository
            var repoNames = await GetRepositoryNamesAsync(azureDevOpsUrl, projectName, patToken);
            
            foreach (string repoName in repoNames)
            {
                string cloneUrl = $"{azureDevOpsUrl}/{projectName}/_git/{repoName}";
                string repoPath = System.IO.Path.Combine(cloneDirectory!, repoName);

                Console.WriteLine($"Processing repository: {repoName}");

                if (System.IO.Directory.Exists(repoPath))
                {
                    Console.WriteLine($"Repository {repoName} already exists. Pulling latest changes...");
                    PullRepository(repoPath, patToken);
                }
                else
                {
                    Console.WriteLine($"Cloning repository {repoName} from {cloneUrl}...");
                    CloneRepository(cloneUrl, repoPath, patToken);
                }
            }
        }

        private static async Task<List<string>> GetRepositoryNamesAsync(string? azureDevOpsUrl, string? projectName, string? patToken)
        {
            var repoNames = new List<string>();
            using HttpClient client = new ();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{patToken}")));

            string url = $"{azureDevOpsUrl}/{projectName}/_apis/git/repositories?api-version=7.0";
            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                JsonDocument jsonDoc = JsonDocument.Parse(jsonResponse);
                foreach (JsonElement repo in jsonDoc.RootElement.GetProperty("value").EnumerateArray())
                {
                    string? name = repo.GetProperty("name").GetString();
                    if (name != null) repoNames.Add(name);
                }
            }
            else
            {
                Console.WriteLine($"Failed to retrieve repositories: {response.ReasonPhrase}");
            }

            return repoNames;
        }

        private static void CloneRepository(string cloneUrl, string repoPath, string? patToken)
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
                Repository.Clone(cloneUrl, repoPath, options);
                Console.WriteLine($"Cloned {cloneUrl} to {repoPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clone repository: {ex.Message}");
            }
        }

        private static void PullRepository(string repoPath, string? patToken)
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