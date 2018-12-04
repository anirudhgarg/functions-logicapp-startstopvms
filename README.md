# Start Stop VMs using Azure Functions and Azure LogicApps

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fanirudhgarg%2Ffunctions-logicapp-startstopvms%2Fmaster%2Fazuredeploy.json" target="_blank">
<img src="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/deploytoazure.png"/>
</a>
<a href="http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-quickstart-templates%2Fmaster%2F101-aks%2Fazuredeploy.json" target="_blank">
<img src="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/visualizebutton.png"/>
</a>

This solution allows you to Start/Stop your Azure VM's based on a schedule. It uses Azure Functions and Azure Logic Apps. 

The folloing functionality is available: 

* Ability to Start and Stop Azure VM's for a given Subscription(required) based on a schedule
* Ability to filter the VM's based on a Resource Group
* Ability to filter the VM's based on a Tag 

The implementation uses Azure VM async API's and hence potentially hundreds of VM's can be started/stopped. 

## Instructions: 

Click the Deploy Button 




