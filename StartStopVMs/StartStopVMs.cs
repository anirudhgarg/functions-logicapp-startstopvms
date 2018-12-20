using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure.Management.Compute.Fluent;

namespace StartStopVMs
{
    public class StartStopVMEntry : TableEntity
    {
        public StartStopVMEntry(string pkey, string rkey)
        {
            this.PartitionKey = pkey;
            this.RowKey = rkey;
        }
        
        public string VMName { get; set; }

        public string VMType { get; set; }

        public string VMStartedOrStopped { get; set; }
    }

    public static class StartStopVMs
    {
        static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        static CloudTableClient cloudTableClient = storageAccount.CreateCloudTableClient();
        static CloudTable table = cloudTableClient.GetTableReference("StartStopVMs");

        [FunctionName("StartStopVMsDurable")]
        public static async Task<string> Run([OrchestrationTrigger] DurableOrchestrationContext startStopVMContext)
        {
            string inputData = startStopVMContext.GetInput<string>();
            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(inputData);
            string subscriptionId = data.ContainsKey("subscriptionId") ? data["subscriptionId"] : string.Empty;
            string mode = data.ContainsKey("mode") ? data["mode"] : string.Empty;
            string resourceGroupName = data.ContainsKey("resourceGroupName") ? data["resourceGroupName"] : string.Empty;
            string tag = data.ContainsKey("tag") ? data["tag"] : string.Empty;
            string batchsize = data.ContainsKey("batchsize") ? data["batchsize"] : "10";
            string[] vmlist;
            vmlist = await startStopVMContext.CallActivityAsync<string[]>("GetVMList", (subscriptionId, resourceGroupName, tag));
            int batch = Int32.Parse(batchsize);
            List<Task<string>> tasks = new List<Task<string>>();
            for (int i = 0; i < vmlist.Length; i = i + batch)
            {
                var batchvmlist = new List<string>
                {
                    mode,
                    subscriptionId
                };
                for (int j = 0; j < batch; j++)
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
            builder.AppendLine(string.Format("A total of {0} Virtual Machines were {1} in this run.", vmlist.Length, mode == "start" ? "started" : "stopped"));
            builder.AppendLine();
            builder.AppendLine("These were the names of the Virtual Machines:");
            foreach (Task<string> task in tasks)
            {
                foreach (string vmName in task.Result.Split(','))
                {
                    builder.AppendLine(vmName);
                }               
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
            IComputeManager azure;
            try
            {
                azure = ComputeManager.Configure().Authenticate(credentials, subscriptionId);                
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
            string[] vmlistResourceIds = vmlist.GetInput<string[]>();
            StringBuilder resultText = new StringBuilder();
            string mode = vmlistResourceIds[0];
            string subscriptionId = vmlistResourceIds[1];

            string token = Authenticate().Result;
            AzureCredentials credentials = new AzureCredentials(new TokenCredentials(token), new TokenCredentials(token), string.Empty, AzureEnvironment.AzureGlobalCloud);            
            var azure = ComputeManager.Configure().Authenticate(credentials, subscriptionId);
            var tasks = new List<Task>();
  
            for (int i = 2; i < vmlistResourceIds.Length; i++)
            {
                string vmName = vmlistResourceIds[i].Split('/')[8];
                resultText.Append(vmName);
                resultText.Append(",");
                InsertRow(vmlistResourceIds[i], mode);
                if (vmlistResourceIds[0] == "start")
                {
                    log.LogInformation("Starting vm with resource id {0}", vmlistResourceIds[i]);
                    tasks.Add(azure.VirtualMachines.GetById(vmlistResourceIds[i]).StartAsync());

                }
                else if (vmlistResourceIds[0] == "stop")
                {
                    log.LogInformation("Stopping vm with resource id {0}", vmlistResourceIds[i]);
                    tasks.Add(azure.VirtualMachines.GetById(vmlistResourceIds[i]).DeallocateAsync());
                }
            }      
           
            await Task.WhenAll(tasks);
            return resultText.ToString().TrimEnd(',');
        }
             
        public static void InsertRow(string vmResourceId, string mode)
        {           
            table.CreateIfNotExistsAsync();
            //write it in the form of <subscriptionId>:<resourceGroupName>
            string partitionKey = string.Format("{0}:{1}", vmResourceId.Split('/')[2], vmResourceId.Split('/')[4]);
            var entry = new StartStopVMEntry(partitionKey, string.Format("{0:d21}{1}{2}", DateTimeOffset.MaxValue.UtcDateTime.Ticks - new DateTimeOffset(DateTime.Now).UtcDateTime.Ticks, "-", Guid.NewGuid().ToString()))
            {
                VMName = vmResourceId.Split('/')[8],
                VMType = "",
                VMStartedOrStopped = mode
            };
            table.ExecuteAsync(TableOperation.Insert(entry));            
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
