using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.IO;

namespace Inedo.BuildMasterExtensions.Git.Clients
{
    internal sealed class LilGitClient : GitClientBase
    {
        public LilGitClient(IGitSourceControlProvider provider)
            : base(provider)
        {
        }
        
        protected override string GitExePath
        {
            get
            {
                var remoteExecuter = this.Provider.RemoteMethodExecuter;
                if (remoteExecuter == null)
                    throw new InvalidOperationException("Lilgit not supported on this agent.");

                string asmLocation = this.Provider.RemoteMethodExecuter.InvokeFunc(GetAsmLocation);
                return this.Provider.Agent.CombinePath(asmLocation, "lilgit.exe");
            }
        }

        private static string GetAsmLocation()
        {
            return PathEx.GetDirectoryName(typeof(LilGitClient).Assembly.Location);
        }

        public override IEnumerable<string> EnumBranches(SourceRepository repo)
        {
            var result = this.ExecuteGitCommand(repo, "branches", "\"" + repo.RemoteUrl + "\"");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));

            return result.Output;
        }

        public override void UpdateLocalRepo(SourceRepository repo, string branch, string tag)
        {
            ProcessResults result;

            var refspec = string.Format("refs/heads/{0}", string.IsNullOrEmpty(branch) ? "master" : branch);

            if (string.IsNullOrEmpty(tag))
                result = this.ExecuteGitCommand(repo, "get", "\"" + repo.RemoteUrl + "\"", "\"" + refspec + "\"");
            else
                result = this.ExecuteGitCommand(repo, "gettag", "\"" + repo.RemoteUrl + "\"", "\"" + tag + "\"", "\"" + refspec + "\"");

            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));
        }

        public override void ApplyTag(SourceRepository repo, string tag)
        {
            var result = this.ExecuteGitCommand(repo, "tag", "\"" + repo.RemoteUrl + "\"", "\"" + tag + "\"", "BuildMaster", "\"Tagged by BuildMaster\"");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));
        }

        public override GitCommit GetLastCommit(SourceRepository repo, string branch)
        {
            var result = this.ExecuteGitCommand(repo, "lastcommit");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));

            var revStr = string.Join(string.Empty, result.Output.ToArray()).Trim();
            return new GitCommit(revStr);
        }

        public override void CloneRepo(SourceRepository repo)
        {
            var result = this.ExecuteGitCommand(repo, "clone", "\"" + repo.RemoteUrl + "\"");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));
        }

        public override void ValidateConnection(SourceRepository repo)
        {
            if (repo != null)
            {
                var result = this.ExecuteGitCommand(repo, "lastcommit");
                if (result.ExitCode != 0)
                    throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));
            }
        }
    }
}
