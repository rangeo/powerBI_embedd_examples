using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using Microsoft.Rest;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace RowLevelSecurityWithCustomData.Services {

  public class EmbeddedReportViewModel {
    public string Id;
    public string Name;
    public string EmbedUrl;
    public string Token;
    public string Role;
  }

  public class PowerBiServiceApi {

    private ITokenAcquisition tokenAcquisition { get; }
    private string urlPowerBiServiceApiRoot { get; }
    private Guid workspaceId { get; }
    private Guid reportId { get; }

    public PowerBiServiceApi(IConfiguration configuration, ITokenAcquisition tokenAcquisition) {
      this.urlPowerBiServiceApiRoot = configuration["PowerBi:ServiceRootUrl"];
      this.workspaceId = new Guid(configuration["PowerBi:WorkspaceId"]);
      this.reportId = new Guid(configuration["PowerBi:CustomDataReportId"]);
      this.tokenAcquisition = tokenAcquisition;
    }

    public const string powerbiApiDefaultScope = "https://analysis.windows.net/powerbi/api/.default";


    //get the Access token from AAD ( azure active directory) , mentioned clearly in AAD_token_generate.cs file. 
    public string GetAccessToken() {
      return this.tokenAcquisition.GetAccessTokenForAppAsync(powerbiApiDefaultScope).Result;
    }

    public PowerBIClient GetPowerBiClient() {
      string accessToken = GetAccessToken();
      var tokenCredentials = new TokenCredentials(accessToken, "Bearer");
      return new PowerBIClient(new Uri(urlPowerBiServiceApiRoot), tokenCredentials);
    }

// Generating Embedd token with RLS data

    public async Task<EmbeddedReportViewModel> GetReport2() {
      PowerBIClient pbiClient = GetPowerBiClient();

      // call to Power BI Service API to get embedding data
      var report = await pbiClient.Reports.GetReportInGroupAsync(this.workspaceId, this.reportId);
      var datasetId = report.DatasetId;

      var tokenRequest = new GenerateTokenRequestV2 {
        LifetimeInMinutes = 15,


        // get the dataset ID for the report
        Datasets = new List<GenerateTokenRequestV2Dataset>() {
          new GenerateTokenRequestV2Dataset(datasetId)
        },


        // get the report ID for this particular user
        Reports = new List<GenerateTokenRequestV2Report>() {
          new GenerateTokenRequestV2Report(reportId, true)
        },

        //get the powerbi workspace ID for this particular user
        TargetWorkspaces = new List<GenerateTokenRequestV2TargetWorkspace>() {
          new GenerateTokenRequestV2TargetWorkspace(workspaceId)
        },

        // Create effective identity object with all the parameters we want to pass for Embedd token request
        Identities = new List<EffectiveIdentity>() {
          new EffectiveIdentity {
            Username = "user1.relayr.io",
            Datasets = new List<string>() { datasetId },
            CustomData = "berlin,munich",
            Roles = new List<string>() { "StatesRole" }
          }
        }
      };

  // generate embed token
        // specify the access level ,edit/view options
      TokenAccessLevel tokenAccessLevel = CustomizationEnabled ? TokenAccessLevel.Edit : TokenAccessLevel.View;
      //request for Embedd token
      var tokenRequest = new GenerateTokenRequest(tokenAccessLevel, datasetId, effectiveIdentity);
      var embedTokenResponse = await pbiClient.Reports.GenerateTokenAsync(WorkspaceId, RlsReportId, tokenRequest);
      var embedToken = embedTokenResponse.Token;

      // return report embedding data to caller
      return new EmbeddedReportViewModel {
        Id = report.Id.ToString(),
        EmbedUrl = report.EmbedUrl,
        Name = report.Name,
        Token = embedToken,
        CustomizationEnabled = CustomizationEnabled
      };
    }


