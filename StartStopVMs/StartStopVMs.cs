using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace StartStopVMs
{
    public static class StartStopVMs
    {
        [FunctionName("StartStopVMsDurable")]
        public static async Task<string> Run([OrchestrationTrigger] DurableOrchestrationContext startStopVMContext)
        {
            string inputData = startStopVMContext.GetInput<string>();
            var data = JsonConvert.DeserializeObject <Dictionary<string, string>>(inputData);
            string subscriptionId = data.ContainsKey("subscriptionId") ? data["subscriptionId"] : string.Empty;
            string mode = data.ContainsKey("mode") ? data["mode"] : string.Empty;
            string resourceGroupName = data.ContainsKey("resourceGroupName") ? data["resourceGroupName"] : string.Empty;
            string tag = data.ContainsKey("tag") ? data["tag"] : string.Empty;
            string batchsize = data.ContainsKey("batchsize") ? data["batchsize"] : "10";
            string[] vmlist;           
            vmlist = await startStopVMContext.CallActivityAsync<string[]>("GetVMList", (subscriptionId, resourceGroupName, tag));                     
            int batch = Int32.Parse(batchsize);
            List<Task<string>> tasks = new List<Task<string>>();
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
                tasks.Add(startStopVMContext.CallActivityAsync<string>("StartStopVMList", batchvmlist.ToArray()));
            }
            await Task.WhenAll(tasks);

            var builder = new StringBuilder();            
            foreach(Task<string> task in tasks)
            {
                builder.AppendLine(task.Result);
            }
            return builder.ToString();
        }

        [FunctionName("GetVMList")]
        public static string[] GetVMList([ActivityTrigger] DurableActivityContext inputs, ILogger log)
        {
            List<string> vmListNames = new List<string>();
            (string subscriptionId, string resourceGroupName, string tag) = inputs.GetInput<(string, string, string)>();
            string token = Authenticate().Result;
            AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), string.Empty, AzureEnvironment.AzureGlobalCloud);
            IAzure azure;
            try
            {
                azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription(subscriptionId);                
            }
            catch(Exception ex)
            {
                throw new Exception("There was an issue authenticating to Azure. Please check your subscriptionId and if MSI has been set up correctly", ex);
            }

            var vmList = !string.IsNullOrEmpty(resourceGroupName)
                        ? azure.VirtualMachines.ListByResourceGroup(resourceGroupName)
                        : azure.VirtualMachines.List();

            foreach (var vm in vmList)
            {
                if ((!string.IsNullOrEmpty(tag) && vm.Tags.ContainsKey(tag)) || string.IsNullOrEmpty(tag))
                {
                    vmListNames.Add(vm.Id);
                }
            }

            return vmListNames.ToArray();
        }

        [FunctionName("StartStopVMList")]
        public static async Task<string> StartStopVMList([ActivityTrigger] DurableActivityContext vmlist, ILogger log)
        {            
            string[] vmlistNames = vmlist.GetInput<string[]>();
            StringBuilder resultText = new StringBuilder();

            string token = Authenticate().Result;
            AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), string.Empty, AzureEnvironment.AzureGlobalCloud);            
            var azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription(vmlistNames[1]);            
            List<Task> tasks = new List<Task>();
            for (int i = 2; i < vmlistNames.Length; i++)
            {                
                if (vmlistNames[0] == "start")
                {
                    log.LogInformation("Starting vm {0}", vmlistNames[i]);
                    tasks.Add(azure.VirtualMachines.GetById(vmlistNames[i]).StartAsync());
                    resultText.AppendLine(string.Format("Started vm {0}", vmlistNames[i].Split('/')[8]));
                }
                else if (vmlistNames[0] == "stop")
                {
                    log.LogInformation("Stopping vm {0}", vmlistNames[i]);
                    tasks.Add(azure.VirtualMachines.GetById(vmlistNames[i]).DeallocateAsync());
                    resultText.AppendLine(string.Format("Stopped vm {0}", vmlistNames[i].Split('/')[8]));
                }
            }
            await Task.WhenAll(tasks);
            return resultText.ToString();
        }

        /// <summary>
        /// Function which receives the initial input and request to Start and Stop VMs
        /// </summary>
        /// <param name="req"></param>
        /// <param name="starter"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("StartStopVMs")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.           
            string eventData = await req.Content.ReadAsStringAsync();

            //check for input data
                //should be valid json
                //subscriptionId and mode are required
                //subscriptionId, mode, resourceGroupName, tag, batchsize are the only keys in the json
                //subscripitonId should be guid, mode should be start or stop
                //batchsize should be between 1 and 100
            try
            {
                Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(eventData);
                string subscriptionId = data.ContainsKey("subscriptionId") ? data["subscriptionId"] : string.Empty;
                string mode = data.ContainsKey("mode") ? data["mode"] : string.Empty;
                if(string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(mode))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, string.Format(@"subscriptionId and mode are required, Usage is: {{""subscriptionId"":""<subscriptionid>"", ""mode"":""start|stop"", [""resourceGroupName"":""<resourceGroupName>""], [""tag"":""<tag>""], [""batchsize"":""<batchsize>""]}}"));
                }
                foreach(var key in data.Keys)
                {
                    if(!key.Equals("subscriptionId") && !key.Equals("mode") && !key.Equals("resourceGroupName") && !key.Equals("tag") && !key.Equals("batchsize"))
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest, string.Format("Found unxpected input {0}, Usage is: {{\"subscriptionId\":\"<subscriptionid>\", \"mode\":\"start|stop\", [\"resourceGroupName\":\"<resourceGroupName>\"], [\"tag\":\"<tag>\"], [\"batchsize\":\"<batchsize>\"]}}", key));
                    }

                    if (key.Equals("subscriptionId"))
                    {
                        if (!Guid.TryParse(data["subscriptionId"], out Guid subId))
                        {
                            return req.CreateResponse(HttpStatusCode.BadRequest, string.Format("subscriptionId should be a guid"));
                        }
                    }

                    if (key.Equals("mode"))
                    {
                        if (data["mode"]!="start" && data["mode"]!="stop")
                        {
                            return req.CreateResponse(HttpStatusCode.BadRequest, string.Format("mode can only have a value of start or stop"));
                        }
                    }

                    if (key.Equals("batchsize"))
                    {
                        if (!Int32.TryParse(data["batchsize"], out int batchsize) || (batchsize <= 0 || batchsize > 100))
                        {
                            return req.CreateResponse(HttpStatusCode.BadRequest, string.Format("batchsize should be a positive number between 1 and 100"));
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                return (req.CreateResponse(HttpStatusCode.BadRequest, string.Format("Error parsing input, Error:{0}, Usage is: {{\"subscriptionId\":\"<subscriptionid>\", \"mode\":\"start|stop\", [\"resourceGroupName\":\"<resourceGroupName>\"], [\"tag\":\"<tag>\"], [\"batchsize\":\"<batchsize>\"]}}", ex.Message)));
            }
            
            // input looks good, start the orchestrator
            string instanceId = await starter.StartNewAsync("StartStopVMsDurable", eventData);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            var response = starter.CreateCheckStatusResponse(req, instanceId);
            return response;
        }

        public static async Task<string> Authenticate()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").ConfigureAwait(false);
            return accessToken;
        }
    }
}
