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
using System.Collections.Generic;
using Microsoft.Azure.Management.Compute.Fluent;
using System.Text;
using System;

namespace StartStopVMs
{    
    public static class StartStopVMs
    {
        [FunctionName("StartStopVMs")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string subscriptionId = req.Query["subscriptionId"];
            string resourceGroupName = req.Query["resourceGroupName"];
            string tag = req.Query["tag"];
            string mode = req.Query["mode"];
            StringBuilder resultText = new StringBuilder();
           
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);
            if (data.ContainsKey("mode"))
            {
                mode = data["mode"];
            }
            if (data.ContainsKey("subscriptionId"))
            {
                subscriptionId = data["subscriptionId"];
            }
            if (data.ContainsKey("resourceGroupName"))
            {
                resourceGroupName = data["resourceGroupName"];
            }
            if (data.ContainsKey("tag"))
            {
                tag = data["tag"];
            }           

            if(string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(mode))
            {
                return new BadRequestObjectResult(@"subscriptionId and mode are required. Usage: {""subscriptionId"":""<subscriptionid>"", ""mode"":""start|stop"", [""resourceGroupName"":""<resourceGroupName>""], [""tag"":""<tag>""]  ");
            }

            string resultstring = requestBody.ToString();
            int numberOfVmsAffected = 0;
            try
            {
                string token = Authenticate().Result;
                AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), string.Empty, AzureEnvironment.AzureGlobalCloud);
                //subscriptionId: 4dda6ad2-730a-4053-88d1-0fa7ff209aea
                //resourceGroupName: 
                var azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription(subscriptionId);
           
            
                var vmList = !string.IsNullOrEmpty(resourceGroupName)
                    ? azure.VirtualMachines.ListByResourceGroup(resourceGroupName)
                    : azure.VirtualMachines.List();

                List<Task> tasks = new List<Task>();               
                foreach (var vm in vmList)
                {                   
                    if ( (!string.IsNullOrEmpty(tag) && vm.Tags.ContainsKey(tag)) || string.IsNullOrEmpty(tag) )
                    {
                        Task task = null;
                        if (mode == "start" && (vm.PowerState == PowerState.Stopped || vm.PowerState == PowerState.Deallocated || vm.PowerState == PowerState.Deallocating || vm.PowerState == PowerState.Stopping))
                        {
                            log.LogInformation("Starting vm {0}", vm.Name);
                            resultText.AppendLine(string.Format("Started vm {0}", vm.Name));
                            numberOfVmsAffected++;
                            task =  vm.StartAsync();
                        }
                        else if (mode == "stop")
                        {
                            log.LogInformation("Stopping vm {0}", vm.Name);
                            resultText.AppendLine(string.Format("Stopped vm {0}", vm.Name));
                            numberOfVmsAffected++;
                            task =  vm.DeallocateAsync();
                        }
                        if(task != null) tasks.Add(task);
                    }
                }                    
           
                foreach (Task task in tasks)
                {
                    await task;
                }

            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(string.Format("Invocation failed with exception: {0}", ex.Message));
            }

            resultText.Insert(0, string.Format("Numbers of VM's affected {0}{1}", numberOfVmsAffected, System.Environment.NewLine));
            return new OkObjectResult($"{resultText.ToString()}");                
        }
      
        public static async Task<string> Authenticate()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").ConfigureAwait(false);
            return accessToken;
        }
    }
}
