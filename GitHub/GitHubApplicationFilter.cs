using System;
using Inedo.BuildMaster.Extensibility.IssueTrackerConnections;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.GitHub
{
    [Serializable]
    [CustomEditor(typeof(GitHubApplicationFilterEditor))]
    [PersistFrom("Inedo.BuildMasterExtensions.GitHub.GitHubApplicationFilter,GitHub")]
    public sealed class GitHubApplicationFilter : IssueTrackerApplicationConfigurationBase
    {
        [Persistent]
        public string Owner { get; set; }

        [Persistent]
        public string Repository { get; set; }

        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "Repository: ", new Hilite(this.Repository), ", Owner: ", new Hilite(this.Owner)
            );
        }
    }
}
