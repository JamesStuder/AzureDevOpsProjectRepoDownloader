using System.Collections.Generic;

namespace DevOps.BulkRepoDownloader.Models
{
    public class Config
    {
        // Legacy single‑org fields (kept for backward compatibility and migration)
        public string? BaseUrl { get; set; }
        public string? PAT { get; set; }
        public string? RepoRootLocation { get; set; }
        public List<ProjectConfig> Projects { get; set; } = new();

        // New multi‑org layout
        public List<OrgConfig> Orgs { get; set; } = new();
    }
}