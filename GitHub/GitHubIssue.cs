using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.Extensibility.IssueSources;

namespace Inedo.BuildMasterExtensions.GitHub
{
    [Serializable]
    internal sealed class GitHubIssue : IIssueTrackerIssue
    {
        private Dictionary<string, object> remoteIssue;

        public GitHubIssue(Dictionary<string, object> issue)
        {
            this.remoteIssue = issue;
        }

        public string Id
        {
            get { return this.remoteIssue["number"].ToString(); }
        }
        public string Type
        {
            get
            {
                var labels = this.remoteIssue["labels"] as IEnumerable<Dictionary<string, object>>;
                return labels?.FirstOrDefault()?["name"]?.ToString();
            }
        }
        public string Title
        {
            get { return this.remoteIssue["title"].ToString(); }
        }
        public string Description
        {
            get { return this.remoteIssue["body"].ToString(); }
        }
        public bool IsClosed
        {
            get { return string.Equals(this.Status, "closed", StringComparison.OrdinalIgnoreCase); }
        }
        public string Status
        {
            get { return this.remoteIssue["state"].ToString(); }
        }
        public DateTime SubmittedDate
        {
            get
            {
                var created = this.remoteIssue["created_at"].ToString();
                return DateTime.Parse(created).ToUniversalTime();
            }
        }
        public string Submitter
        {
            get
            {
                var user = this.remoteIssue["user"] as Dictionary<string, object>;
                if (user != null)
                    return user["login"].ToString();
                else
                    return null;
            }
        }
        public string Url
        {
            get { return this.remoteIssue["html_url"].ToString(); }
        }
    }
}
