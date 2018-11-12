using System.IO;
using System.Runtime.Serialization.Json;
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
using System.Collections.Generic;
using Microsoft.Azure.Management.Compute.Fluent;

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
            string tagsToCheck = req.Query["tagsToCheck"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);
            subscriptionId = data["subscriptionId"];
            resourceGroupName = data["resourceGroupName"];
            tagsToCheck = data["tags"];

            string resultstring = requestBody.ToString();
            string token = Authenticate().Result;
            AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), string.Empty, AzureEnvironment.AzureGlobalCloud);
            //subscriptionId: 4dda6ad2-730a-4053-88d1-0fa7ff209aea
            //resourceGroupName: 
            var azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription(subscriptionId);

            var vmList = !string.IsNullOrEmpty(resourceGroupName)
                ? azure.VirtualMachines.ListByResourceGroup(resourceGroupName)
                : azure.VirtualMachines.List();

            if (!string.IsNullOrEmpty(tagsToCheck))
            {               
                foreach (var vm in vmList)
                {
                    if (vm.Tags.ContainsKey(tagsToCheck) && vm.Tags[tagsToCheck].ToLower() == "true")
                    {
                        
                    }
                }
                finalResultVms.IntersectWith(vmsWithTags);
            }          

            foreach(var vm in finalResultVms)
            {
                resultstring += vm.Name;
                resultstring += ",";
            }
            return subscriptionId != null
                ? (ActionResult)new OkObjectResult($"Try, {resultstring}")
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
