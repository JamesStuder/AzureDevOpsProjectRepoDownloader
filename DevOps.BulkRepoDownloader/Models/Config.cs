using System.Collections.Generic;

namespace DevOps.BulkRepoDownloader.Models
{
    public class Config
    {
        public string? BaseUrl { get; set; }
        public string? PAT { get; set; }
        public string? RepoRootLocation { get; set; }
        public List<ProjectConfig> Projects { get; set; } = new();
    }
}