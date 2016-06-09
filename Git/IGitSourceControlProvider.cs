using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMasterExtensions.Git.Clients;

namespace Inedo.BuildMasterExtensions.Git
{
    internal interface IGitSourceControlProvider
    {
        SourceRepository[] Repositories { get; }

        IFileOperationsExecuter Agent { get; }
        
        ProcessResults ExecuteCommandLine(string fileName, string arguments, string workingDirectory);
    }
}
