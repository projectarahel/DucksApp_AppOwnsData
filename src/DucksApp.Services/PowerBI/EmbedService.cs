using DucksApp.Services.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.PowerBI.Api.V2;
using Microsoft.PowerBI.Api.V2.Models;
using Microsoft.Rest;
using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Who.Whedashboard.Services.PowerBI
{
    public class EmbedService : IEmbedService
    {
        private EmbedConfig _embedConfig;
        private TokenCredentials _tokenCredentials;

        private static readonly string TenantId = ConfigurationManager.AppSettings["tenantId"];
        private static readonly string ApplicationId = ConfigurationManager.AppSettings["applicationId"];
        private static readonly string ApplicationSecret = ConfigurationManager.AppSettings["applicationSecret"];

        private static readonly string AuthorityUrl = ConfigurationManager.AppSettings["authorityUrl"];
        private static readonly string ResourceUrl = ConfigurationManager.AppSettings["resourceUrl"];
        private static readonly string ApiUrl = ConfigurationManager.AppSettings["apiUrl"];

        private static readonly string WorkspaceId = ConfigurationManager.AppSettings["workspaceId"];
        private static readonly string ReportId = ConfigurationManager.AppSettings["reportId"];

        private static readonly string DatasetId = ConfigurationManager.AppSettings["datasetId"];

        public EmbedConfig EmbedConfig
        {
            get { return _embedConfig; }
        }

        public EmbedService()
        {
            _tokenCredentials = null;
            _embedConfig = new EmbedConfig();
        }

        private async Task<bool> SetTokenCredentialsAsync()
        {
            // Get token credentials for user
            return await GetTokenCredentialsAsync();
        }

        public async Task SetReportEmbedConfigAsync()
        {
            //// Get token credentials for user
            //var getCredentialsResult = await GetTokenCredentialsAsync();

            if (await SetTokenCredentialsAsync())
            {
                try
                {
                    // Create a Power BI Client object. It will be used to call Power BI APIs.
                    using (var client = new PowerBIClient(new Uri(ApiUrl), _tokenCredentials))
                    {
                        // Get a list of reports.
                        var reports = await client.Reports.GetReportsInGroupAsync(WorkspaceId);

                        // No reports retrieved for the given workspace.
                        if (reports.Value.Count() == 0)
                            _embedConfig.ErrorMessage = "No reports were found in the workspace";

                        Report report;
                        if (string.IsNullOrWhiteSpace(ReportId))
                        {
                            // Get the first report in the workspace.
                            report = reports.Value.FirstOrDefault();
                        }
                        else
                        {
                            report = reports.Value.FirstOrDefault(r => r.Id.Equals(ReportId, StringComparison.InvariantCultureIgnoreCase));
                        }

                        if (report == null)
                            _embedConfig.ErrorMessage = "No report with the given ID was found in the workspace. Make sure ReportId is valid.";

                        var datasets = await client.Datasets.GetDatasetByIdInGroupAsync(WorkspaceId, report.DatasetId);
                        _embedConfig.IsEffectiveIdentityRequired = datasets.IsEffectiveIdentityRequired;
                        _embedConfig.IsEffectiveIdentityRolesRequired = datasets.IsEffectiveIdentityRolesRequired;
                        GenerateTokenRequest generateTokenRequestParameters;

                        generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view");

                        var tokenResponse = await client.Reports.GenerateTokenInGroupAsync(WorkspaceId, report.Id, generateTokenRequestParameters);

                        if (tokenResponse == null)
                            _embedConfig.ErrorMessage = "Failed to generate embed token.";

                        // Generate Embed Configuration.
                        _embedConfig.EmbedToken = tokenResponse;
                        _embedConfig.EmbedUrl = report.EmbedUrl;
                        _embedConfig.Id = report.Id;
                    }
                }
                catch (HttpOperationException exc)
                {
                    _embedConfig.ErrorMessage = string.Format("Status: {0} ({1})\r\nResponse: {2}\r\nRequestId: {3}", exc.Response.StatusCode, (int)exc.Response.StatusCode, exc.Response.Content, exc.Response.Headers["RequestId"].FirstOrDefault());
                }
            }
        }

        private string GetConfigErrors()
        {
            // Application Id must have a value.
            if (string.IsNullOrWhiteSpace(ApplicationId))
            {
                return "ApplicationId is empty. Please register your application as Native app in https://dev.powerbi.com/apps.";
            }

            // Application Id must be a Guid object.
            Guid result;

            if (!Guid.TryParse(ApplicationId, out result))
            {
                return "ApplicationId must be a Guid object. Please register your application as Native app in https://dev.powerbi.com/apps.";
            }

            // Workspace Id must have a value.
            if (string.IsNullOrWhiteSpace(WorkspaceId))
            {
                return "WorkspaceId is empty.";
            }

            // Workspace Id must be a Guid object.
            if (!Guid.TryParse(WorkspaceId, out result))
            {
                return "WorkspaceId must be a Guid object.";
            }

            if (string.IsNullOrWhiteSpace(ApplicationSecret))
            {
                return "ApplicationSecret is empty. Please register your application as Web app.";
            }

            // Must fill tenant Id
            if (string.IsNullOrWhiteSpace(TenantId))
            {
                return "Invalid Tenant.";
            }

            return null;
        }

        private async Task<AuthenticationResult> DoAuthenticationAsync()
        {
            AuthenticationResult authenticationResult = null;

            try
            {
                // For app only authentication, we need the specific tenant id in the authority url
                var tenantSpecificURL = AuthorityUrl.Replace("common", TenantId);

                var authenticationContext = new AuthenticationContext(tenantSpecificURL);

                // Authentication using app credentials
                var credential = new ClientCredential(ApplicationId, ApplicationSecret);

                authenticationResult = await authenticationContext.AcquireTokenAsync(ResourceUrl, credential);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            return authenticationResult;
        }

        private async Task<bool> GetTokenCredentialsAsync()
        {
            var error = GetConfigErrors();
            if (error != null)
            {
                _embedConfig.ErrorMessage = error;
                return false;
            }

            // Authenticate using created credentials
            AuthenticationResult authenticationResult = null;
            try
            {
                authenticationResult = await DoAuthenticationAsync();
            }
            catch (AggregateException exc)
            {
                _embedConfig.ErrorMessage = exc.InnerException.Message;
                return false;
            }

            if (authenticationResult == null)
            {
                _embedConfig.ErrorMessage = "Authentication Failed.";
                return false;
            }

            _tokenCredentials = new TokenCredentials(authenticationResult.AccessToken, "Bearer");
            return true;
        }

        public async Task<bool> RefreshDatasetAsync()
        {
            var hasTokenCredentials = this._tokenCredentials != null;

            if (!hasTokenCredentials)
                hasTokenCredentials = await SetTokenCredentialsAsync();

            if (hasTokenCredentials)
            {
                try
                {
                    // Create a Power BI Client object. It will be used to call Power BI APIs.
                    using (var client = new PowerBIClient(new Uri(ApiUrl), _tokenCredentials))
                    {
                        await client.Datasets.RefreshDatasetInGroupAsync(WorkspaceId, DatasetId);
                        return true;
                    }
                }
                catch (HttpOperationException exc)
                {
                    _embedConfig.ErrorMessage = string.Format("Status: {0} ({1})\r\nResponse: {2}\r\nRequestId: {3}", exc.Response.StatusCode, (int)exc.Response.StatusCode, exc.Response.Content, exc.Response.Headers["RequestId"].FirstOrDefault());
                }
            }

            return false;
        }
    }
}