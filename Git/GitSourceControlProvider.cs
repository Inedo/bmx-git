using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Inedo.Agents;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.Git
{
    /// <summary>
    /// A provider that integrates with the Git source control system.
    /// </summary>
    [DisplayName("Git")]
    [Description("Supports most versions of Git; requires Git to be installed on the server for use with an SSH Agent.")]
    [CustomEditor(typeof(GitSourceControlProviderEditor))]
    public sealed partial class GitSourceControlProvider : DistributedSourceControlProviderBase, IClientCommandProvider, IGitSourceControlProvider
    {
        private GitSourceControlProviderCommon wrappedProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitSourceControlProvider"/> class.
        /// </summary>
        public GitSourceControlProvider()
        {
        }

        /// <summary>
        /// Gets or sets the Git executable path.
        /// </summary>
        [Persistent]
        public string GitExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the name of the branch to fall back on for pre-v3.2 if there is no branch specified in the path.
        /// </summary>
        [Persistent]
        public string BranchName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the standard Git client.
        /// </summary>
        [Persistent]
        public bool UseStandardGitClient { get; set; }

        public override char DirectorySeparator
        {
            get { return '/'; }
        }

        private GitSourceControlProviderCommon WrappedProvider
        {
            get
            {
                if (this.wrappedProvider == null)
                    this.wrappedProvider = new GitSourceControlProviderCommon(this, this.UseStandardGitClient ? this.GitExecutablePath : null);

                return this.wrappedProvider;
            }
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

        public override void ValidateConnection()
        {
            this.WrappedProvider.ValidateConnectionToAllRepositories();
        }

        public override string ToString()
        {
            if (this.Repositories.Length == 1)
                return "Git at " + Util.CoalesceStr(this.Repositories[0].RemoteUrl, this.Repositories[0].Name);
            else
                return "Git";
        }

        public object GetCurrentRevision(string path)
        {
            var context = this.CreateSourceControlContext(path);
            return this.WrappedProvider.GetCurrentRevision(context);
        }

        public void ExecuteClientCommand(string commandName, string arguments)
        {
            if (!this.UseStandardGitClient)
            {
                this.LogError("Client commands only supported when using the standard Git client.");
                return;
            }

            var results = this.ExecuteCommandLine(
                this.GitExecutablePath,
                commandName + " " + arguments,
                null
            );

            foreach (var line in results.Output)
                this.LogInformation(line);

            foreach (var line in results.Error)
                this.LogError(line);
        }

        public IEnumerable<ClientCommand> GetAvailableCommands()
        {
            using (var stream = typeof(GitSourceControlProvider).Assembly.GetManifestResourceStream("Inedo.BuildMasterExtensions.Git.GitCommands.txt"))
            using (var reader = new StreamReader(stream))
            {
                var line = reader.ReadLine();
                while (line != null)
                {
                    var commandInfo = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    yield return new ClientCommand(commandInfo[0].Trim(), commandInfo[1].Trim());

                    line = reader.ReadLine();
                }
            }
        }

        public string GetClientCommandHelp(string commandName)
        {
            if (!this.UseStandardGitClient)
                throw new InvalidOperationException("Client commands only supported when using the standard Git client.");

            try
            {
                var docsPath = Path.Combine(Path.GetDirectoryName(typeof(GitSourceControlProvider).Assembly.Location), "Docs");
                return File.ReadAllText(Path.Combine(docsPath, "git-" + commandName + ".txt"));
            }
            catch (Exception e)
            {
                return "Help not available for the \"" + commandName + "\" command. The specific error message was: " + e.Message;
            }
        }

        public string GetClientCommandPreview()
        {
            return string.Empty;
        }

        public bool SupportsCommandHelp
        {
            get { return true; }
        }

        IFileOperationsExecuter IGitSourceControlProvider.Agent
        {
            get { return this.Agent.GetService<IFileOperationsExecuter>(); }
        }

        IRemoteMethodExecuter IGitSourceControlProvider.RemoteMethodExecuter
        {
            get { return this.Agent.GetService<IRemoteMethodExecuter>(); }
        }

        Clients.ProcessResults IGitSourceControlProvider.ExecuteCommandLine(string fileName, string arguments, string workingDirectory)
        {
            // This is not an ideal way to do this, but the idea here is to show an error if this is not used on a Windows client
            // Currently, only the Windows agent is an IRemoteCommandExecuter, so this will work
            if (!this.UseStandardGitClient && !(this.Agent is IRemoteMethodExecuter))
                throw new NotAvailableException("The integrated Git client cannot be used on Linux. Enable the Use Standard Git Client option of the Git Provider.");

            var results = this.ExecuteCommandLine(fileName, arguments, workingDirectory);
            return new Clients.ProcessResults(results.ExitCode, results.Output, results.Error);
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
            throw new NotImplementedException();
        }

        public override object GetCurrentRevision(SourceControlContext context)
        {
            return this.WrappedProvider.GetCurrentRevision(context);
        }

        public override void GetLatest(SourceControlContext context, string targetPath)
        {
            this.WrappedProvider.GetLatest(context, targetPath);
        }

        public override void GetLabeled(string label, SourceControlContext context, string targetPath)
        {
            this.WrappedProvider.GetLabeled(label, context, targetPath);
        }

        public override void UpdateLocalWorkspace(SourceControlContext context)
        {
            this.WrappedProvider.UpdateLocalRepository(context, null);
        }

        public override void DeleteWorkspace(SourceControlContext context)
        {
            this.WrappedProvider.DeleteWorkspace(context);
        }

        public override IEnumerable<string> EnumerateLabels(SourceControlContext context)
        {
            return this.WrappedProvider.EnumerateLabels(context);
        }
    }
}
