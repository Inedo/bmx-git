using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using Inedo.BuildMaster;
using Newtonsoft.Json;

namespace Inedo.BuildMasterExtensions.GitHub
{
    internal sealed class GitHub
    {
        static GitHub()
        {
            // Ensure TLS 1.2 is supported. See https://github.com/blog/2498-weak-cryptographic-standards-removal-notice
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
        }

        public const string GitHubComUrl = "https://api.github.com";

        private string apiBaseUrl;

        public string OrganizationName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public GitHub(string apiBaseUrl)
        {
            this.apiBaseUrl = Util.CoalesceStr(apiBaseUrl, GitHub.GitHubComUrl).TrimEnd('/');
        }

        public IEnumerable<Dictionary<string, object>> EnumRepositories()
        {
            UriBuilder url;
            if (!string.IsNullOrEmpty(this.OrganizationName))
                url = new UriBuilder(string.Format("{0}/orgs/{1}/repos?per_page=500", this.apiBaseUrl, HttpUtility.UrlEncode(this.OrganizationName)));
            else
                url = new UriBuilder(string.Format("{0}/user/repos?per_page=500", this.apiBaseUrl));

            var results = (IEnumerable<object>)this.Invoke("GET", url.ToString());
            return results.Cast<Dictionary<string, object>>();
        }

        public IEnumerable<Dictionary<string, object>> EnumIssues(string milestone, string ownerName, string repositoryName)
        {
            int? milestoneNumber = this.FindMilestone(milestone, ownerName, repositoryName, false);

            if (milestoneNumber == null)
                return Enumerable.Empty<Dictionary<string, object>>();

            return this.EnumIssues((int)milestoneNumber, ownerName, repositoryName);
        }
        public IEnumerable<Dictionary<string, object>> EnumIssues(int milestoneNumber, string ownerName, string repositoryName)
        {
            var openIssues = (IEnumerable<object>)this.Invoke("GET", string.Format("{0}/repos/{1}/{2}/issues?milestone={3}&state=open", this.apiBaseUrl, ownerName, repositoryName, milestoneNumber));
            var closedIssues = (IEnumerable<object>)this.Invoke("GET", string.Format("{0}/repos/{1}/{2}/issues?milestone={3}&state=closed", this.apiBaseUrl, ownerName, repositoryName, milestoneNumber));

            return openIssues
                .Cast<Dictionary<string, object>>()
                .Concat(closedIssues.Cast<Dictionary<string, object>>());
        }
        public Dictionary<string, object> GetIssue(string issueId, string ownerName, string repositoryName)
        {
            return (Dictionary<string, object>)this.Invoke("GET", string.Format("{0}/repos/{1}/{2}/issues/{3}", this.apiBaseUrl, ownerName, repositoryName, issueId));
        }
        public void UpdateIssue(string issueId, string ownerName, string repositoryName, object update)
        {
            this.Invoke("PATCH", string.Format("{0}/repos/{1}/{2}/issues/{3}", this.apiBaseUrl, ownerName, repositoryName, issueId), update);
        }

        public void CreateMilestone(string milestone, string ownerName, string repositoryName)
        {
            var url = string.Format("{0}/repos/{1}/{2}/milestones", this.apiBaseUrl, ownerName, repositoryName);
            int? milestoneNumber = this.FindMilestone(milestone, ownerName, repositoryName, false);
            if (milestoneNumber != null)
            {
                // Milestone already exists
                return;
            }

            this.Invoke(
                "POST",
                url,
                new { title = milestone }
            );
        }
        public void CloseMilestone(string milestone, string ownerName, string repositoryName)
        {
            var url = string.Format("{0}/repos/{1}/{2}/milestones", this.apiBaseUrl, ownerName, repositoryName);
            int? milestoneNumber = this.FindMilestone(milestone, ownerName, repositoryName, false);
            if (milestoneNumber == null)
            {
                // Milestone not found
                return;
            }

            this.Invoke(
                "PATCH",
                url + "/" + milestoneNumber,
                new { state = "closed" }
            );
        }

        public void CreateComment(string issueId, string ownerName, string repositoryName, string commentText)
        {
            this.Invoke(
                "POST",
                string.Format("{0}/repos/{1}/{2}/issues/{3}/comments", this.apiBaseUrl, Uri.EscapeDataString(ownerName), Uri.EscapeDataString(repositoryName), Uri.EscapeDataString(issueId)),
                new
                {
                    body = commentText
                }
            );
        }

        private int? FindMilestone(string title, string ownerName, string repositoryName, bool likelyClosed)
        {
            var openMilestones = this.EnumMilestones(ownerName, repositoryName, "open");
            var closedMilestones = this.EnumMilestones(ownerName, repositoryName, "closed");

            IEnumerable<Dictionary<string, object>> milestones;
            if (likelyClosed)
                milestones = closedMilestones.Concat(openMilestones);
            else
                milestones = openMilestones.Concat(closedMilestones);

            return milestones
                .Where(m => string.Equals((m["title"] ?? "").ToString(), title, StringComparison.OrdinalIgnoreCase))
                .Select(m => m["number"] as int?)
                .FirstOrDefault();
        }
        private IEnumerable<Dictionary<string, object>> EnumMilestones(string ownerName, string repositoryName, string state)
        {
            // Implemented using an iterator just to make it lazy
            var milestones = (IEnumerable<object>)this.Invoke("GET", string.Format("{0}/repos/{1}/{2}/milestones?state={3}", this.apiBaseUrl, ownerName, repositoryName, state));
            if (milestones == null)
                yield break;
            foreach (Dictionary<string, object> obj in milestones)
                yield return obj;
        }

        private object Invoke(string method, string url)
        {
            return this.Invoke(method, url, null);
        }
        private object Invoke(string method, string url, object data)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.UserAgent = "BuildMasterGitHubExtension/" + typeof(GitHub).Assembly.GetName().Version.ToString();
            request.Method = method;
            if (data != null)
            {
                using (var requestStream = request.GetRequestStream())
                using (var writer = new StreamWriter(requestStream, new UTF8Encoding(false)))
                {
                    JsonSerializer.CreateDefault().Serialize(writer, data);
                }
            }

            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers[HttpRequestHeader.Authorization] = "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(this.UserName + ":" + this.Password));

            try
            {
                using (var response = request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    var js = new JavaScriptSerializer();
                    return js.DeserializeObject(reader.ReadToEnd());
                }
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                    throw;

                using (var responseStream = ex.Response.GetResponseStream())
                {
                    throw new Exception(new StreamReader(responseStream).ReadToEnd());
                }
            }
        }
    }
}
