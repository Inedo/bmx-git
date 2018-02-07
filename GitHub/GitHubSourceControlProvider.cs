using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMasterExtensions.Git;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.GitHub
{
    /// <summary>
    /// A provider that integrates with the Git source control system that is optimized for GitHub.
    /// </summary>
    [DisplayName("GitHub")]
    [Description("Git integration optimized for use with GitHub.com or GitHub Enterprise; requires Git to be installed on the server for use with an SSH Agent.")]
    [CustomEditor(typeof(GitHubSourceControlProviderEditor))]
    public sealed class GitHubSourceControlProvider : DistributedSourceControlProviderBase, IGitSourceControlProvider
    {
        private GitSourceControlProviderCommon wrappedProvider;
        private Lazy<SourceRepository[]> repositories;
        private Lazy<GitHub> github;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubSourceControlProvider"/> class.
        /// </summary>
        public GitHubSourceControlProvider()
        {
            this.github = new Lazy<GitHub>(() => new GitHub(this.ApiUrl) { OrganizationName = this.OrganizationName, UserName = this.UserName, Password = this.Password });
            this.repositories = new Lazy<SourceRepository[]>(
                () => this.GitHub
                    .EnumRepositories()
                    .Select(r => CreateSourceRepository(r, this.UserName, this.Password))
                    .ToArray()
            );
        }

        /// <summary>
        /// Gets or sets the Git executable path.
        /// </summary>
        /// <remarks>
        /// If not specified, the built-in client will be used.
        /// </remarks>
        [Persistent]
        public string GitExecutablePath { get; set; }
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
        /// Gets or sets a value indicating whether to use the standard Git client.
        /// </summary>
        [Persistent]
        public bool UseStandardGitClient { get; set; }
        /// <summary>
        /// Gets or sets the API URL.
        /// </summary>
        [Persistent]
        public string ApiUrl { get; set; }

        private GitSourceControlProviderCommon WrappedProvider
        {
            get
            {
                if (this.wrappedProvider == null)
                    this.wrappedProvider = new GitSourceControlProviderCommon(this, this.UseStandardGitClient ? this.GitExecutablePath : null);

                return this.wrappedProvider;
            }
        }

        /// <summary>
        /// Gets a value indicating whether to display the multiple repository editor.
        /// </summary>
        public override bool DisplayEditor { get { return false; } }

        public override char DirectorySeparator
        {
            get { return '/'; }
        }

        private GitHub GitHub
        {
            get { return this.github.Value; }
        }

        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            var context = this.CreateSourceControlContext(sourcePath);
            return this.WrappedProvider.GetDirectoryEntryInfo(context);
        }

        public override byte[] GetFileContents(string filePath)
        {
            return this.WrappedProvider.GetFileContents(filePath);
        }

        public override bool IsAvailable()
        {
            return this.WrappedProvider.IsAvailable();
        }

        public override string ToString()
        {
            var s = new StringBuilder("GitHub");
            
            if (!string.IsNullOrEmpty(this.OrganizationName))
                s.AppendFormat(" ({0} organization)", this.OrganizationName);
            
            if (!string.IsNullOrEmpty(this.UserName))
                s.AppendFormat(" using {0}", this.UserName);

            return s.ToString();
        }

        public override void ValidateConnection()
        {
            this.WrappedProvider.ValidateConnectionToFirstRepository();
        }

        public object GetCurrentRevision(string path)
        {
            var context = this.CreateSourceControlContext(path);
            return this.WrappedProvider.GetCurrentRevision(context);
        }
        
        SourceRepository[] IGitSourceControlProvider.Repositories
        {
            get { return this.repositories.Value; }
        }

        IFileOperationsExecuter IGitSourceControlProvider.Agent
        {
            get { return this.Agent.GetService<IFileOperationsExecuter>(); }
        }

        IRemoteMethodExecuter IGitSourceControlProvider.RemoteMethodExecuter
        {
            get { return this.Agent.GetService<IRemoteMethodExecuter>(); }
        }

        Git.Clients.ProcessResults IGitSourceControlProvider.ExecuteCommandLine(string fileName, string arguments, string workingDirectory)
        {
            // This is not an ideal way to do this, but the idea here is to show an error if this is not used on a Windows client
            // Currently, only the Windows agent is an IRemoteCommandExecuter, so this will work
            if (!this.UseStandardGitClient && !(this.Agent is IRemoteMethodExecuter))
                throw new NotAvailableException("The integrated Git client cannot be used on Linux. Enable the Use Standard Git Client option of the GitHub Provider.");

            var results = this.ExecuteCommandLine(fileName, arguments, workingDirectory);
            return new Git.Clients.ProcessResults(results.ExitCode, results.Output, results.Error);
        }

        public override void ApplyLabel(string label, SourceControlContext context)
        {
            this.WrappedProvider.ApplyLabel(label, context);
        }

        public override SourceControlContext CreateSourceControlContext(object contextData)
        {
            return new GitPath(this, (string)contextData);
        }

        public override void EnsureLocalWorkspace(SourceControlContext context)
        {
            this.WrappedProvider.EnsureLocalRepository(context);
        }

        public override IEnumerable<string> EnumerateBranches(SourceControlContext context)
        {
            return this.WrappedProvider.EnumerateBranches(context);
        }

        public override void ExportFiles(SourceControlContext context, string targetDirectory)
        {
            this.WrappedProvider.ExportFiles(context, targetDirectory);
        }

        public override object GetCurrentRevision(SourceControlContext context)
        {
            return this.WrappedProvider.GetCurrentRevision(context);
        }

        public override void GetLabeled(string label, SourceControlContext context, string targetPath)
        {
            this.WrappedProvider.GetLabeled(label, context, targetPath);
        }

        public override void UpdateLocalWorkspace(SourceControlContext context)
        {
            this.WrappedProvider.UpdateLocalRepository(context, null);
        }

        private static SourceRepository CreateSourceRepository(Dictionary<string, object> repo, string userName, string password)
        {
            var repository = new SourceRepository();

            repository.Name = (string)repo["name"];
            var url = new UriBuilder((string)repo["clone_url"]);
            url.UserName = Uri.EscapeDataString(userName);
            url.Password = Uri.EscapeDataString(password);
            repository.RemoteUrl = url.ToString();

            return repository;
        }

        public override void DeleteWorkspace(SourceControlContext context)
        {
            this.WrappedProvider.DeleteWorkspace(context);
        }

        public override IEnumerable<string> EnumerateLabels(SourceControlContext context)
        {
            return this.WrappedProvider.EnumerateLabels(context);
        }

        public override void GetLatest(SourceControlContext context, string targetPath)
        {
            this.WrappedProvider.GetLatest(context, targetPath);
        }
    }
}
