using System.Collections.Generic;

namespace DevOps.BulkRepoDownloader.Models
{
    public class ProjectConfig
    {
        public string? Name { get; set; }
        public List<string> Repositories { get; set; } = new();
    }
}