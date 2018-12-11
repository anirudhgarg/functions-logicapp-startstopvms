using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StartStopVMs
{
    public static class StartStopVMs
    {
        [FunctionName("StartStopVMs")]
        public static async Task Run([OrchestrationTrigger] DurableOrchestrationContext startStopVMContext)
        {
            Dictionary<string, string> data = startStopVMContext.GetInput<Dictionary<string, string>>();                    
            string subscriptionId = data.ContainsKey("subscriptionId") ? data["subscriptionId"] : string.Empty;
            string mode = data.ContainsKey("mode") ? data["mode"] : string.Empty;
            string resourceGroupName = data.ContainsKey("resourceGroupName") ? data["resourceGroupName"] : string.Empty;
            string tag = data.ContainsKey("tag") ? data["tag"] : string.Empty;
            string batchsize = data.ContainsKey("batchsize") ? data["batchsize"] : "10";

            batchsize = "3";
            string[] vmlist = await startStopVMContext.CallActivityAsync<string[]>("GetVMList", (subscriptionId, resourceGroupName, tag));
            int batch = Int32.Parse(batchsize);
            List<Task> tasks = new List<Task>();
            for (int i=0; i<vmlist.Length; i=i+batch)
            {
                List<string> batchvmlist = new List<string>();
                batchvmlist.Add(mode);
                batchvmlist.Add(subscriptionId);
                for(int j=0; j<batch; j++)
                {
                    if ((i + j) < vmlist.Length)
                    {
                        batchvmlist.Add(vmlist[i + j]);
                    }
                }
                tasks.Add(startStopVMContext.CallActivityAsync<string[]>("StartStopVMList", batchvmlist.ToArray()));
            }
            await Task.WhenAll(tasks);
        }

        [FunctionName("GetVMList")]
        public static string[] GetVMList([ActivityTrigger] DurableActivityContext inputs, ILogger log)
        {
            List<string> vmListNames = new List<string>();
            (string subscriptionId, string resourceGroupName, string tag) inputInfo = inputs.GetInput<(string, string, string)>();
            string token = Authenticate().Result;
            AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), string.Empty, AzureEnvironment.AzureGlobalCloud);
            //subscriptionId: 4dda6ad2-730a-4053-88d1-0fa7ff209aea
            //resourceGroupName: 
            var azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription(inputInfo.subscriptionId);

            var vmList = !string.IsNullOrEmpty(inputInfo.resourceGroupName)
                ? azure.VirtualMachines.ListByResourceGroup(inputInfo.resourceGroupName)
                : azure.VirtualMachines.List();
            
            foreach (var vm in vmList)
            {
                if ((!string.IsNullOrEmpty(inputInfo.tag) && vm.Tags.ContainsKey(inputInfo.tag)) || string.IsNullOrEmpty(inputInfo.tag))
                {
                    vmListNames.Add(vm.Id);
                }
            }
            return vmListNames.ToArray();
        }

        [FunctionName("StartStopVMList")]
        public static async Task StartStopVMList([ActivityTrigger] DurableActivityContext vmlist, ILogger log)
        {            
            string[] vmlistNames = vmlist.GetInput<string[]>();
            string token = Authenticate().Result;
            AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), string.Empty, AzureEnvironment.AzureGlobalCloud);
            //subscriptionId: 4dda6ad2-730a-4053-88d1-0fa7ff209aea
            //resourceGroupName: 
            var azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription(vmlistNames[1]);
            log.LogInformation("***********Logging In**********");
            for (int i = 2; i < vmlistNames.Length; i++)
            {                
                if (vmlistNames[0] == "start")
                {
                    log.LogInformation("Starting vm {0}", vmlistNames[i]);
                    await azure.VirtualMachines.GetById(vmlistNames[i]).StartAsync();
                }
                else if (vmlistNames[0] == "stop")
                {
                    log.LogInformation("Stopping vm {0}", vmlistNames[i]);
                    await azure.VirtualMachines.GetById(vmlistNames[i]).DeallocateAsync();
                }
            }            
        }

        public static async Task<string> Authenticate()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").ConfigureAwait(false);
            return accessToken;
        }
    }
}
