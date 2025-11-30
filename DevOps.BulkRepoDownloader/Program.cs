using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
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
         
                if (string.IsNullOrWhiteSpace(_Config.RepoRootLocation) || _Config.Orgs.Count == 0)
                {
                    Console.WriteLine("Configuration file is missing required fields. Reinitializing...");
                    _Config = await InitializeConfigInteractiveAsync(_ConfigPath);
                }
                else
                {
                    await RefreshProjectsIfChangedAsync(_Config);
                }
            }
            else
            {
                _Config = await InitializeConfigInteractiveAsync(_ConfigPath);
            }
            
            bool? showJson = await AskYesNoWithTimeoutAsync(
                "Would you like to display the current configuration JSON for copying? (y/n) [auto-skip in 10s]: ",
                TimeSpan.FromSeconds(10));
            if (showJson == true)
            {
                Console.WriteLine();
                Console.WriteLine("===== Current Configuration (copyable JSON) =====");
                Console.WriteLine(SerializeConfig(_Config!));
                Console.WriteLine("===== End Configuration =====");
                Console.WriteLine();
            }
            
            bool useOrgFolder = _Config.Orgs.Count > 1;

            foreach (OrgConfig org in _Config.Orgs)
            {
                string orgFolder = GetOrgFolderName(org.BaseUrl);
                foreach (ProjectConfig project in org.Projects)
                {
                    if (string.IsNullOrWhiteSpace(project.Name))
                    {
                        continue;
                    }
                    string projectDir = useOrgFolder
                        ? Path.Combine(_Config.RepoRootLocation!, orgFolder, project.Name!)
                        : Path.Combine(_Config.RepoRootLocation!, project.Name!);
                    Directory.CreateDirectory(projectDir);

                    foreach (string repoName in project.Repositories)
                    {
                        if (string.IsNullOrWhiteSpace(repoName))
                        {
                            continue;
                        }
                        string baseUrl = org.BaseUrl!.TrimEnd('/');
                        string encodedProject = Uri.EscapeDataString(project.Name!);
                        string encodedRepo = Uri.EscapeDataString(repoName);
                        string cloneUrl = $"{baseUrl}/{encodedProject}/_git/{encodedRepo}";
                        string repoPath = Path.Combine(projectDir, repoName);

                        Console.WriteLine($"Processing repository: {project.Name}/{repoName}");

                        if (Directory.Exists(Path.Combine(repoPath, ".git")))
                        {
                            Console.WriteLine($"Repository {repoName} already exists. Pulling latest changes...");
                            _RepoAccess.PullRepository(repoPath, org.PAT);
                        }
                        else if (Directory.Exists(repoPath))
                        {
                            Console.WriteLine($"Directory exists but not a git repository. Attempting to clone anyway...");
                            _RepoAccess.CloneRepository(cloneUrl, repoPath, org.PAT);
                        }
                        else
                        {
                            Console.WriteLine($"Cloning repository {repoName} from {cloneUrl}...");
                            _RepoAccess.CloneRepository(cloneUrl, repoPath, org.PAT);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initializes and guides the user through an interactive process for creating a new configuration.
        /// Prompts the user to input the root directory for cloning repositories and optionally specify
        /// multiple Azure DevOps organizations. The generated configuration is then saved to the specified path.
        /// </summary>
        /// <param name="configPath">The file path where the configuration will be saved.</param>
        /// <returns>A task that resolves to the created <see cref="Config"/> object containing the user's input.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the required input for the repository root location is not provided by the user.
        /// </exception>
        private static async Task<Config> InitializeConfigInteractiveAsync(string configPath)
        {
            Console.Write("Enter the root directory where you want to clone the repositories: ");
            string? rootDirectory = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new InvalidOperationException("RepoRootLocation is required.");
            }

            Config config = new()
            {
                RepoRootLocation = rootDirectory,
                Orgs = new List<OrgConfig>()
            };

            bool addMore;
            do
            {
                OrgConfig org = await CollectOrgAsync();
                config.Orgs.Add(org);
                addMore = AskYesNo("Do you want to add another Azure DevOps organization? (y/n): ");
            } while (addMore);

            await _FileAccess!.SaveConfigAsync(configPath, config);
            Console.WriteLine($"Configuration saved to {configPath}");
            return config;
        }

        /// <summary>
        /// Collects configuration details for an Azure DevOps organization, including its base URL,
        /// Personal Access Token (PAT), and the list of projects and their associated repositories.
        /// The method prompts the user for input and performs the necessary API calls to retrieve
        /// project and repository information.
        /// </summary>
        /// <returns>An <see cref="OrgConfig"/> object containing the organization's base URL, PAT,
        /// and configurations for selected projects.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required inputs such as the base URL
        /// or PAT are missing or invalid.</exception>
        private static async Task<OrgConfig> CollectOrgAsync()
        {
            Console.Write("Enter your Azure DevOps organization URL (e.g., https://dev.azure.com/your_organization): ");
            string? azureDevOpsUrl = Console.ReadLine();
            Console.Write("Enter your Personal Access Token (PAT): ");
            string? patToken = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(azureDevOpsUrl) || string.IsNullOrWhiteSpace(patToken))
            {
                throw new InvalidOperationException("BaseUrl and PAT are required.");
            }
            string normalizedBase = azureDevOpsUrl.TrimEnd('/');

            // Fetch projects
            List<string> allProjects = await _DevOpsAccess!.GetProjectsAsync(normalizedBase, patToken);
            List<string> selectedProjects;
            bool? all = await AskYesNoWithTimeoutAsync("Include ALL projects from this organization? (y/n) [auto-skip in 10s]: ", TimeSpan.FromSeconds(10));
            if (all == null || all == true)
            {
                // New file and no answer within timeout -> include all
                selectedProjects = allProjects;
            }
            else
            {
                // Allow 10s to filter; if no input, default to ALL
                selectedProjects = await PromptSelectManyWithTimeoutAsync(
                    allProjects,
                    "Select projects by number (e.g., 1,3-5) [auto-skip in 10s keeps ALL]: ",
                    TimeSpan.FromSeconds(10),
                    preselected: null,
                    defaultOnTimeout: allProjects
                );
            }

            List<ProjectConfig> projectEntries = new();
            foreach (string project in selectedProjects)
            {
                List<string> repos = await _DevOpsAccess.GetRepositoryNamesAsync(normalizedBase, project, patToken);
                projectEntries.Add(new ProjectConfig { Name = project, Repositories = repos });
            }

            return new OrgConfig
            {
                BaseUrl = normalizedBase,
                PAT = patToken,
                Projects = projectEntries
            };
        }

        /// <summary>
        /// Updates the project's configuration if changes are detected in the project lists
        /// for the configured organizations. Prompts the user to select updated projects
        /// and saves the new configuration if modifications are approved.
        /// </summary>
        /// <param name="config">An instance of the <c>Config</c> class containing
        /// organizational details, project information, and repository settings.</param>
        /// <returns>A task that represents the asynchronous operation of checking and
        /// updating project configurations.</returns>
        /// <exception cref="ArgumentNullException">Thrown when required fields in the
        /// configuration, such as organization data or personal access tokens, are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when project retrieval
        /// or saving the configuration fails due to unexpected errors.</exception>
        private static async Task RefreshProjectsIfChangedAsync(Config config)
        {
            bool updated = false;
            foreach (OrgConfig org in config.Orgs)
            {
                string baseUrl = org.BaseUrl!.TrimEnd('/');
                List<string> current = await _DevOpsAccess!.GetProjectsAsync(baseUrl, org.PAT!);
                HashSet<string> currentSet = new(current, StringComparer.OrdinalIgnoreCase);
                HashSet<string> storedSet = new(org.Projects.ConvertAll(p => p.Name ?? string.Empty), StringComparer.OrdinalIgnoreCase);
                if (currentSet.SetEquals(storedSet))
                {
                    continue;
                }

                bool? change = await AskYesNoWithTimeoutAsync($"Projects changed for org '{GetOrgFolderName(org.BaseUrl)}'. Do you want to update the selection? (y/n) [auto-skip in 10s keeps current]: ", TimeSpan.FromSeconds(10));
                if (change != true)
                {
                    continue;
                }

                // Give 10s to adjust selection; on timeout keep currently selected ones
                List<string> defaultKeep = new List<string>(storedSet);
                List<string> selected = await PromptSelectManyWithTimeoutAsync(
                    current,
                    "Select projects by number (e.g., 1,3-5) [auto-skip in 10s keeps current]: ",
                    TimeSpan.FromSeconds(10),
                    preselected: storedSet,
                    defaultOnTimeout: defaultKeep
                );
                
                List<ProjectConfig> newProjects = new();
                foreach (string proj in selected)
                {
                    ProjectConfig? existing = org.Projects.Find(p => string.Equals(p.Name, proj, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        newProjects.Add(existing);
                    }
                    else
                    {
                        List<string> repos = await _DevOpsAccess.GetRepositoryNamesAsync(baseUrl, proj, org.PAT);
                        newProjects.Add(new ProjectConfig { Name = proj, Repositories = repos });
                    }
                }
                org.Projects = newProjects;
                updated = true;
            }

            if (updated)
            {
                await _FileAccess!.SaveConfigAsync(_ConfigPath!, config);
                Console.WriteLine("Configuration updated with new project selections.");
            }
        }

        /// <summary>
        /// Prompts the user with a yes or no question and reads their response.
        /// </summary>
        /// <param name="prompt">The message to display to the user, asking for a yes or no response.</param>
        /// <returns><c>true</c> if the user responds with "y" or "yes"; otherwise, <c>false</c> for "n" or "no".</returns>
        private static bool AskYesNo(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }
                input = input.Trim().ToLowerInvariant();
                switch (input)
                {
                    case "y" or "yes":
                        return true;
                    case "n" or "no":
                        return false;
                }
            }
        }

        /// <summary>
        /// Prompts the user with a yes/no question and waits for a response. If the user does not respond
        /// within the specified timeout, the method automatically skips the prompt and returns null.
        /// </summary>
        /// <param name="prompt">The message displayed to the user, requesting a yes/no response.</param>
        /// <param name="timeout">The duration to wait for user input before skipping the prompt.</param>
        /// <returns>
        /// A nullable boolean value indicating the user's response: true for "yes", false for "no", and
        /// null if no input was provided within the timeout period or if an invalid response was entered.
        /// </returns>
        private static async Task<bool?> AskYesNoWithTimeoutAsync(string prompt, TimeSpan timeout)
        {
            Console.Write(prompt);
            Task<string?> inputTask = Task.Run(Console.ReadLine);
            Task completed = await Task.WhenAny(inputTask, Task.Delay(timeout));
            if (completed != inputTask)
            {
                Console.WriteLine();
                return null;
            }

            string? input = inputTask.Result;
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }
            input = input.Trim().ToLowerInvariant();
            return input switch
            {
                "y" or "yes" => true,
                "n" or "no" => false,
                _ => null
            };
        }

        /// <summary>
        /// Converts the configuration object into a JSON string representation.
        /// The resulting JSON string is indented for readability.
        /// </summary>
        /// <param name="cfg">The configuration object to be serialized.</param>
        /// <returns>A string containing the JSON representation of the configuration object.</returns>
        private static string SerializeConfig(Config cfg)
        {
            return JsonSerializer.Serialize(cfg, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Prompts the user to select multiple items from a list by entering their corresponding indices or ranges.
        /// Displays all available items alongside their indices and allows preselected items to be marked.
        /// </summary>
        /// <param name="items">The list of items from which selections can be made.</param>
        /// <param name="prompt">The prompt message displayed to the user requesting input.</param>
        /// <param name="preselected">A set of items that should be marked as preselected, or null if none are preselected.</param>
        /// <returns>A list of items selected by the user based on their input.</returns>
        /// <exception cref="FormatException">Thrown if the user's input cannot be correctly parsed into valid indices or ranges.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the user's input references indices outside the valid range of items.</exception>
        private static List<string> PromptSelectMany(List<string> items, string prompt,
            HashSet<string>? preselected = null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                bool isSel = preselected != null && preselected.Contains(items[i]);
                Console.WriteLine($"{i + 1}. {items[i]}{(isSel ? " [selected]" : string.Empty)}");
            }
            while (true)
            {
                Console.Write(prompt);
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }
                try
                {
                    HashSet<int> indices = ParseSelection(input.Trim(), items.Count);
                    List<string> selected = new();
                    foreach (int idx in indices)
                    {
                        selected.Add(items[idx]);
                    }
                    return selected;
                }
                catch
                {
                    Console.WriteLine("Invalid selection. Try again.");
                }
            }
        }

        /// <summary>
        /// Like PromptSelectMany but with a timeout that returns a default selection if no input is provided in time.
        /// </summary>
        private static async Task<List<string>> PromptSelectManyWithTimeoutAsync(
            List<string> items,
            string prompt,
            TimeSpan timeout,
            HashSet<string>? preselected = null,
            List<string>? defaultOnTimeout = null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                bool isSel = preselected != null && preselected.Contains(items[i]);
                Console.WriteLine($"{i + 1}. {items[i]}{(isSel ? " [selected]" : string.Empty)}");
            }
            while (true)
            {
                Console.Write(prompt);
                Task<string?> inputTask = Task.Run(Console.ReadLine);
                Task completed = await Task.WhenAny(inputTask, Task.Delay(timeout));
                if (completed != inputTask)
                {
                    Console.WriteLine();
                    return defaultOnTimeout ?? new List<string>();
                }

                string? input = inputTask.Result;
                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultOnTimeout ?? new List<string>();
                }
                try
                {
                    HashSet<int> indices = ParseSelection(input.Trim(), items.Count);
                    List<string> selected = new();
                    foreach (int idx in indices)
                    {
                        selected.Add(items[idx]);
                    }
                    return selected;
                }
                catch
                {
                    Console.WriteLine("Invalid selection. Try again.");
                }
            }
        }

        /// <summary>
        /// Parses a string input that represents selected indices or ranges of indices, and converts it into
        /// a set of zero-based integers representing valid selections within the specified range.
        /// </summary>
        /// <param name="input">A comma-separated string containing individual indices or ranges specified as "start-end".</param>
        /// <param name="count">The total count of items available for selection, used to validate the indices or ranges provided in the input.</param>
        /// <returns>A set of zero-based integers representing the parsed and validated selections.</returns>
        /// <exception cref="FormatException">Thrown when the input contains improperly formatted ranges.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the input contains indices or ranges outside the valid selection range.</exception>
        private static HashSet<int> ParseSelection(string input, int count)
        {
            HashSet<int> set = new();
            string[] parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                if (part.Contains('-'))
                {
                    string[] range = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (range.Length != 2)
                    {
                        throw new FormatException();
                    }
                    int start = int.Parse(range[0]);
                    int end = int.Parse(range[1]);
                    if (start < 1 || end < 1 || start > end || end > count)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                    for (int i = start - 1; i <= end - 1; i++) set.Add(i);
                }
                else
                {
                    int idx = int.Parse(part);
                    if (idx < 1 || idx > count)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                    set.Add(idx - 1);
                }
            }
            return set;
        }

        /// <summary>
        /// Generates a folder name based on the organization URL. If the URL is null or invalid, a default name
        /// is returned. The method ensures that the resulting folder name is sanitized to comply with file system
        /// naming conventions.
        /// </summary>
        /// <param name="baseUrl">The base URL of the organization. This may be null or improperly formatted.</param>
        /// <returns>A sanitized folder name derived from the organization's URL. If the URL is null or invalid,
        /// a default name ("org") is returned.</returns>
        private static string GetOrgFolderName(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return "org";
            }
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
            {
                return SanitizePath(baseUrl);
            }
            string orgName = uri.AbsolutePath.Trim('/');
            if (string.IsNullOrWhiteSpace(orgName))
            {
                orgName = uri.Host;
            }
            return SanitizePath(orgName);
        }

        /// <summary>
        /// Sanitizes a provided string to make it compliant with file system naming conventions.
        /// Replaces any invalid characters with an underscore.
        /// </summary>
        /// <param name="name">The name or path to be sanitized. This string may contain characters that are invalid for file systems.</param>
        /// <returns>A sanitized version of the input string, with all invalid characters replaced by underscores.</returns>
        private static string SanitizePath(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}