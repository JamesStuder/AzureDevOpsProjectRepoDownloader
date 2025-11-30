using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DevOps.BulkRepoDownloader.DataAccess
{
    public class DevOpsAccess
    {
        private static readonly HttpClient _httpClient = new ();
        private static readonly MediaTypeWithQualityHeaderValue _jsonMediaType = new("application/json");

        /// <summary>
        /// Creates an HTTP GET request with the appropriate headers for accessing the Azure DevOps API.
        /// </summary>
        /// <param name="url">The URL of the Azure DevOps API endpoint.</param>
        /// <param name="patToken">The personal access token (PAT) used for authentication with the Azure DevOps API.</param>
        /// <returns>An <see cref="HttpRequestMessage"/> object configured for an HTTP GET request with authentication headers.</returns>
        private static HttpRequestMessage CreateGet(string url, string patToken)
        {
            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(_jsonMediaType);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{patToken}"))
            );
            return request;
        }

        /// <summary>
        /// Retrieves a list of project names from an Azure DevOps organization.
        /// </summary>
        /// <param name="azureDevOpsUrl">The URL of the Azure DevOps organization.</param>
        /// <param name="patToken">The personal access token (PAT) used for authentication with the Azure DevOps API.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of project names accessible in the specified Azure DevOps organization.</returns>
        public async Task<List<string>> GetProjectsAsync(string azureDevOpsUrl, string patToken)
        {
            List<string> projectNames = new List<string>();
            string baseUrl = azureDevOpsUrl.TrimEnd('/');
            string url = $"{baseUrl}/_apis/projects?api-version=7.0";
            using HttpRequestMessage request = CreateGet(url, patToken);
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                using JsonDocument jsonDoc = JsonDocument.Parse(jsonResponse);
                foreach (JsonElement proj in jsonDoc.RootElement.GetProperty("value").EnumerateArray())
                {
                    string? name = proj.GetProperty("name").GetString();
                    if (!string.IsNullOrWhiteSpace(name)) projectNames.Add(name);
                }
            }
            else
            {
                Console.WriteLine($"Failed to retrieve projects: {response.ReasonPhrase}");
            }

            return projectNames;
        }

        /// <summary>
        /// Retrieves the list of repository names for a specific project in an Azure DevOps organization.
        /// </summary>
        /// <param name="azureDevOpsUrl">The URL of the Azure DevOps organization.</param>
        /// <param name="projectName">The name of the project for which the repository names are to be retrieved.</param>
        /// <param name="patToken">The personal access token (PAT) used for authentication with the Azure DevOps API.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of repository names for the specified project.</returns>
        public async Task<List<string>> GetRepositoryNamesAsync(string? azureDevOpsUrl, string? projectName, string? patToken)
        {
            List<string> repoNames = new List<string>();
            string baseUrl = azureDevOpsUrl!.TrimEnd('/');
            string encodedProject = Uri.EscapeDataString(projectName!);
            string url = $"{baseUrl}/{encodedProject}/_apis/git/repositories?api-version=7.0";
            using HttpRequestMessage request = CreateGet(url, patToken!);
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                using JsonDocument jsonDoc = JsonDocument.Parse(jsonResponse);
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
    }
}