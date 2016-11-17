using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.GitHub
{
    internal sealed class GitHubSourceControlProviderEditor : ProviderEditorBase
    {
        private ValidatingTextBox txtOrganizationName;
        private ValidatingTextBox txtUserName;
        private PasswordTextBox txtPassword;
        private ValidatingTextBox txtApiUrl;
        private CheckBox chkUseStandardGitClient;
        private FileBrowserTextBox txtGitExecutablePath;

        public GitHubSourceControlProviderEditor()
        {
            this.ValidateBeforeSave += this.GitHubSourceControlProviderEditor_ValidateBeforeSave;
        }

        public override void BindToForm(ProviderBase extension)
        {
            var provider = (GitHubSourceControlProvider)extension;

            this.txtGitExecutablePath.Text = provider.GitExecutablePath;
            this.txtOrganizationName.Text = provider.OrganizationName;
            this.txtUserName.Text = provider.UserName;
            this.txtPassword.Text = provider.Password;
            this.chkUseStandardGitClient.Checked = provider.UseStandardGitClient;
            this.txtApiUrl.Text = provider.ApiUrl;
        }

        public override ProviderBase CreateFromForm()
        {
            return new GitHubSourceControlProvider
            {
                GitExecutablePath = this.txtGitExecutablePath.Text,
                OrganizationName = this.txtOrganizationName.Text,
                UserName = this.txtUserName.Text,
                Password = this.txtPassword.Text,
                UseStandardGitClient = this.chkUseStandardGitClient.Checked,
                ApiUrl = this.txtApiUrl.Text
            };
        }

        protected override void CreateChildControls()
        {
            this.txtApiUrl = new ValidatingTextBox() { DefaultText = GitHub.GitHubComUrl };

            this.chkUseStandardGitClient = new CheckBox
            {
                Text = "Use Standard Git Client"
            };

            this.txtGitExecutablePath = new FileBrowserTextBox
            {
                ServerId = this.EditorContext.ServerId,
                IncludeFiles = true,
                Required = false
            };

            this.txtOrganizationName = new ValidatingTextBox { Required = false, DefaultText = "Optional" };
            this.txtUserName = new ValidatingTextBox { Required = true };
            this.txtPassword = new PasswordTextBox { Required = true };

            var ctlExePathField = new SlimFormField("Git executable path:", this.txtGitExecutablePath);

            this.Controls.Add(
                new SlimFormField("API base URL:", this.txtApiUrl)
                {
                    HelpText = "This provider connects to github.com by default. If connecting to GitHub Enterprise on a local network, specify the hostname of the API here."
                },
                new SlimFormField("Organization name:", this.txtOrganizationName),
                new SlimFormField("GitHub username:", this.txtUserName),
                new SlimFormField("GitHub password:", this.txtPassword),
                new SlimFormField("Git client:", this.chkUseStandardGitClient)
                {
                    HelpText = "This extension includes a lightweight Git client for Windows. To use an alternate Git client, check the box and provide the path of the other client."
                },
                ctlExePathField
            );

            this.Controls.BindVisibility(this.chkUseStandardGitClient, ctlExePathField);
        }

        private void GitHubSourceControlProviderEditor_ValidateBeforeSave(object sender, ValidationEventArgs<ProviderBase> e)
        {
            if (this.chkUseStandardGitClient.Checked && string.IsNullOrEmpty(this.txtGitExecutablePath.Text))
            {
                e.ValidLevel = ValidationLevel.Error;
                e.Message = "You must provide a Git client to use if not using the built-in Git client.";
            }
        }
    }
}
