using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.BuildMaster.Extensibility.IssueTrackerConnections;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;
using Inedo.Extensibility.IssueSources;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.GitHub
{
    /// <summary>
    /// Issue tracking provider for GitHub.
    /// </summary>
    [DisplayName("GitHub")]
    [Description("Provides issue tracking integration for GitHub.")]
    [CustomEditor(typeof(GitHubIssueTrackingProviderEditor))]
    [PersistFrom("Inedo.BuildMasterExtensions.GitHub.GitHubIssueTrackingProvider,GitHub")]
    public sealed partial class GitHubIssueTrackingProvider : IssueTrackerConnectionBase, IReleaseManager, IIssueCloser, IIssueCommenter
    {
        private Lazy<GitHub> github;

        public GitHubIssueTrackingProvider()
        {
            this.github = new Lazy<GitHub>(() => new GitHub(this.ApiUrl) { OrganizationName = this.OrganizationName, UserName = this.UserName, Password = this.Password });
        }

        /// <summary>
        /// Gets or sets the organization name which owns the repositories.
        /// </summary>
        [Persistent]
        public string OrganizationName { get; set; }
        /// <summary>
        /// Gets or sets the user name to use to authenticate with GitHub.
        /// </summary>
        [Persistent]
        public string UserName { get; set; }
        /// <summary>
        /// Gets or sets the password of the user name to use to authenticate with GitHub.
        /// </summary>
        [Persistent]
        public string Password { get; set; }
        /// <summary>
        /// Gets or sets base URL for the API.
        /// </summary>
        [Persistent]
        public string ApiUrl { get; set; }

        private GitHub GitHub
        {
            get { return this.github.Value; }
        }

        public override RichDescription GetDescription()
        {
            if (!string.IsNullOrWhiteSpace(this.OrganizationName))
            {
                return new RichDescription(
                    "GitHub (",
                    new Hilite(this.OrganizationName),
                    ")"
                );
            }
            else
            {
                return new RichDescription("GitHub");
            }
        }
        public override IEnumerable<IIssueTrackerIssue> EnumerateIssues(IssueTrackerConnectionContext context)
        {
            var filter = this.GetFilter(context);

            return this.GitHub
                .EnumIssues(context.ReleaseNumber, filter.Owner, filter.Repository)
                .Select(i => new GitHubIssue(i));
        }
        public override bool IsAvailable()
        {
            // Always available - no special client installation required
            return true;
        }
        public override void ValidateConnection()
        {
            try
            {
                this.GetRepositories().FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new NotAvailableException(ex.Message, ex);
            }
        }
        public override IssueTrackerApplicationConfigurationBase GetDefaultApplicationConfiguration(int applicationId)
        {
            return new GitHubApplicationFilter();
        }

        void IReleaseManager.CreateRelease(IssueTrackerConnectionContext context)
        {
            var filter = this.GetFilter(context);
            this.GitHub.CreateMilestone(context.ReleaseNumber, filter.Owner, filter.Repository);
        }
        void IReleaseManager.DeployRelease(IssueTrackerConnectionContext context)
        {
            var filter = this.GetFilter(context);
            this.GitHub.CloseMilestone(context.ReleaseNumber, filter.Owner, filter.Repository);
        }

        void IIssueCloser.CloseAllIssues(IssueTrackerConnectionContext context)
        {
            var issues = this.EnumerateIssues(context).ToList();
            var filter = this.GetFilter(context);
            foreach (var issue in issues)
            {
                this.GitHub.UpdateIssue(
                    issue.Id,
                    filter.Owner,
                    filter.Repository,
                    new { state = "closed" }
                );
            }
        }
        void IIssueCloser.CloseIssue(IssueTrackerConnectionContext context, string issueId)
        {
            var filter = this.GetFilter(context);

            this.GitHub.UpdateIssue(
                issueId,
                filter.Owner,
                filter.Repository,
                new { state = "closed" }
            );
        }

        void IIssueCommenter.AddComment(IssueTrackerConnectionContext context, string issueId, string commentText)
        {
            var filter = this.GetFilter(context);
            this.GitHub.CreateComment(issueId, filter.Owner, filter.Repository, commentText);
        }

        internal IEnumerable<GitHubRepository> GetRepositories()
        {
            return this.GitHub
                .EnumRepositories()
                .Select(r => new GitHubRepository(((Dictionary<string, object>)r["owner"])["login"].ToString(), r["name"].ToString()));
        }

        private GitHubApplicationFilter GetFilter(IssueTrackerConnectionContext context)
        {
            return (GitHubApplicationFilter)context.ApplicationConfiguration ?? this.legacyFilter;
        }
    }
}
