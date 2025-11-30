using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DevOps.BulkRepoDownloader.DataAccess;
using DevOps.BulkRepoDownloader.Models;
using FileAccess = DevOps.BulkRepoDownloader.DataAccess.FileAccess;


namespace DevOps.BulkRepoDownloader
{
    public class Program
    {
        private const string _DefaultConfigFileName = "devops.config.json";
        private static string? _ConfigPath;
        private static Config? _Config;
        private static DevOpsAccess? _DevOpsAccess;
        private static FileAccess? _FileAccess;
        private static RepoAccess? _RepoAccess;

        /// <summary>
        /// Entry point for the application that manages the bulk cloning and pulling of repositories
        /// from Azure DevOps based on a configuration file. This method initializes required resources,
        /// loads or creates the configuration file, and processes repositories for each project specified
        /// in the configuration.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the application. Currently not used.</param>
        /// <returns>A task that represents the asynchronous execution of the program.</returns>
        /// <exception cref="IOException">Thrown when issues occur with file loading or saving.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the configuration file is missing required fields.</exception>
        private static async Task Main(string[] args)
        {
            _ConfigPath = Path.Combine(AppContext.BaseDirectory, _DefaultConfigFileName);
            _Config = new Config();
            _DevOpsAccess = new DevOpsAccess();
            _FileAccess = new FileAccess();
            _RepoAccess = new RepoAccess();

            if (File.Exists(_ConfigPath))
            {
                Console.WriteLine($"Loading configuration from {_ConfigPath}...");
                _Config = await _FileAccess.LoadConfigAsync(_ConfigPath);
                if (_Config.BaseUrl is null || _Config.PAT is null || _Config.RepoRootLocation is null)
                {
                    Console.WriteLine("Configuration file is missing required fields. Reinitializing...");
                    _Config = await InitializeConfigAsync(_ConfigPath);
                }
            }
            else
            {
                _Config = await InitializeConfigAsync(_ConfigPath);
            }
            
            foreach (ProjectConfig project in _Config.Projects)
            {
                if (string.IsNullOrWhiteSpace(project.Name)) continue;
                string projectDir = Path.Combine(_Config.RepoRootLocation!, project.Name!);
                Directory.CreateDirectory(projectDir);

                foreach (string repoName in project.Repositories)
                {
                    if (string.IsNullOrWhiteSpace(repoName)) continue;
                    string baseUrl = _Config.BaseUrl!.TrimEnd('/');
                    string encodedProject = Uri.EscapeDataString(project.Name!);
                    string encodedRepo = Uri.EscapeDataString(repoName);
                    string cloneUrl = $"{baseUrl}/{encodedProject}/_git/{encodedRepo}";
                    string repoPath = Path.Combine(projectDir, repoName);

                    Console.WriteLine($"Processing repository: {project.Name}/{repoName}");

                    if (Directory.Exists(Path.Combine(repoPath, ".git")))
                    {
                        Console.WriteLine($"Repository {repoName} already exists. Pulling latest changes...");
                        _RepoAccess.PullRepository(repoPath, _Config.PAT);
                    }
                    else if (Directory.Exists(repoPath))
                    {
                        Console.WriteLine($"Directory exists but not a git repository. Attempting to clone anyway...");
                        _RepoAccess.CloneRepository(cloneUrl, repoPath, _Config.PAT);
                    }
                    else
                    {
                        Console.WriteLine($"Cloning repository {repoName} from {cloneUrl}...");
                        _RepoAccess.CloneRepository(cloneUrl, repoPath, _Config.PAT);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes and saves a configuration file asynchronously by prompting the user
        /// for Azure DevOps organization URL, Personal Access Token (PAT), and the root
        /// directory for cloning repositories. Retrieves project and repository information
        /// and stores it in the configuration file.
        /// </summary>
        /// <param name="configPath">The file path where the configuration should be saved.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the initialized <see cref="Config"/> object.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required fields (BaseUrl, PAT, or RepoRootLocation) are missing or invalid.</exception>
        private static async Task<Config> InitializeConfigAsync(string configPath)
        {
            Console.Write("Enter your Azure DevOps organization URL (e.g., https://dev.azure.com/your_organization): ");
            string? azureDevOpsUrl = Console.ReadLine();

            Console.Write("Enter your Personal Access Token (PAT): ");
            string? patToken = Console.ReadLine();

            Console.Write("Enter the root directory where you want to clone the repositories: ");
            string? rootDirectory = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(azureDevOpsUrl) || string.IsNullOrWhiteSpace(patToken) || string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new InvalidOperationException("BaseUrl, PAT, and RepoRootLocation are required.");
            }
            
            string normalizedBase = azureDevOpsUrl.TrimEnd('/');
            List<string> projects = await _DevOpsAccess!.GetProjectsAsync(normalizedBase, patToken);
            List<ProjectConfig> projectEntries = new List<ProjectConfig>();
            foreach (string project in projects)
            {
                List<string> repos = await _DevOpsAccess.GetRepositoryNamesAsync(normalizedBase, project, patToken);
                projectEntries.Add(new ProjectConfig { Name = project, Repositories = repos });
            }

            Config config = new ()
            {
                BaseUrl = normalizedBase,
                PAT = patToken,
                RepoRootLocation = rootDirectory,
                Projects = projectEntries
            };

            await _FileAccess!.SaveConfigAsync(configPath, config);
            Console.WriteLine($"Configuration saved to {configPath}");
            return config;
        }
    }
}