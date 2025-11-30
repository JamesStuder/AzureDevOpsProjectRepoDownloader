using System.Collections.Generic;

namespace DevOps.BulkRepoDownloader.Models
{
    public class OrgConfig
    {
        public string? BaseUrl { get; set; }
        public string? PAT { get; set; }
        public List<ProjectConfig> Projects { get; set; } = new();
    }
}
