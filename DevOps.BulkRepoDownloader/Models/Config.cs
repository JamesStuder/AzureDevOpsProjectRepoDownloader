using System.Collections.Generic;

namespace DevOps.BulkRepoDownloader.Models
{
    public class Config
    {
        public string? RepoRootLocation { get; set; }
        
        public List<OrgConfig> Orgs { get; set; } = new();
    }
}