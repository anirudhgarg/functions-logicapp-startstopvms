# Start Stop VMs using Azure Functions and Azure LogicApps

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fanirudhgarg%2Ffunctions-logicapp-startstopvms%2Fmaster%2Fazuredeploy.json" target="_blank">
<img src="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/deploytoazure.png"/>
</a>
<a href="http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-quickstart-templates%2Fmaster%2F101-aks%2Fazuredeploy.json" target="_blank">
<img src="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/visualizebutton.png"/>
</a>

This solution allows you to Start/Stop your Azure VM's based on a schedule. It uses Azure Functions and Azure Logic Apps. 

The following functionality is available: 

* Ability to Start and Stop Azure VM's for a given Subscription(required) based on a schedule
* Ability to filter the VM's based on a Resource Group
* Ability to filter the VM's based on a Tag 

The implementation uses Azure VM async API's and hence potentially hundreds of VM's can be started/stopped. 

## Instructions: 

This solution creates an Azure Function and two Azure Logic Apps. 

### Deployment of Azure Resources
Click the Deploy to Azure Button, this brings up the dialog below in the Azure Portal, choose the subscription and resource group that you want to create your Azure Function and Logic Apps in. Finally choose the name of the "App". This will be used to create the name of the Function and the Logic Apps.

![Image of Deployment](https://github.com/anirudhgarg/functions-logicapps-startstopvms-bytes/blob/master/DeploymentScreen.jpg)

### Configuring Logic Apps
Once deployment is done you will see that several Azure Resources are created. The Start VM and Stop VM functionality is driven through two Logic Apps [AppName]-StartVMs and [AppName]-StopVMs. 
  


### Give the required permission to the MSI 


