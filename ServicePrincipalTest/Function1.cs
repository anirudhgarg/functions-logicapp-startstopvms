using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;

namespace ServicePrincipalTest
{
    /*
     */
    public static class StartStopVM
    {
        [FunctionName("ServicePrincipalTest")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string subscriptionId = req.Query["subscriptionId"];
            string resourceGroupName = req.Query["resourceGroupName"];            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            resourceGroupName = resourceGroupName ?? data?.resourceGroupName;


            string token = Authenticate().Result;
            string tenantId = System.Environment.GetEnvironmentVariable("TenantId", System.EnvironmentVariableTarget.Process);
            AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), tenantId, AzureEnvironment.AzureGlobalCloud);
            var azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription("4dda6ad2-730a-4053-88d1-0fa7ff209aea");
            var vms = azure.VirtualMachines.List();
            string vmName = "default";
            foreach(var vm in vms)
            {
                vmName = vm.Name;
                break;
            }

            return resourceGroupName != null
                ? (ActionResult)new OkObjectResult($"Hello, {vmName}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        public static async Task<string> Authenticate()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").ConfigureAwait(false);
            return accessToken;
        }
    }
}
