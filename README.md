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

![Image of Deployment](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/DeploymentScreen.jpg)

### Give the required permission to the Functions App MSI 
The Function App needs permissions on the VM's to perform  the start, stop action on them. The deployment already creates a MSI with the [AppName]. Based on whether you want to want to give permission to this MSI for a whole Subscription, ResoureGroup or even VM's you have  to add the "Virtual Machine Contributor" role assignment to the MSI for it. This can be done in the Azure Portal by navigating to the appropriate scope and then to the "Access Control (IAM)" and click the "Add a role assignment". Note that this Subscription/Resource Group/VM's can be in a different Subscription/Resource Group that you have depoyed the app too. 

![Image of AddRoleAssignment](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/Add-RoleAssignment.jpg)

This opens the dialog box for the Add role assignment. Choose the role "Virtual Machine Contributor" and search for the [AppName] in the Select and select the MSI and click Save. This is the way that you can control access. 

![Image of RoleAssignment](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/RoleAssignment.jpg)

### Configuring Logic Apps
Once deployment is done you will see that several Azure Resources are created. The Start VM and Stop VM functionality is driven through two Logic Apps [AppName]-StartVMs and [AppName]-StopVMs. 

Navigate to the Logic Apps section of the Azure Portal and find the Logic Apps created. For each of them, Click through the Logic App and click Edit and Click Code View.

![Image of LogicApps](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/LogicAppsScreen.jpg)

#### Configure the Schedule that you want the Start or Stop VM's to take place
Click on the first activity where you enter the schedule for when the VM's need to be started or stopped and edit it accordingly. Currently the Start VM Logic App is configured to run at 7:00 AM Pacific Time and the Stop VM Logic App is configured to run at 7:00 PM Pacific Time.

![Image of LogicApps](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/Schedule.jpg)

#### Configure the Azure Subscription, Azure Resource Group (optional) and Tags (optional) 

* SubcriptionId:
Click on the Enter SubscriptionId activity and in the Value field enter the Azure Subscription Id that you want to start or stop VM's for (note that the SubscriptionId that you deployed your resources to is already prepopulated, if this is the same Subscription whose VM's you want to Start/Stop then leave that)

* Resource Group:
Optionally, enter the value of a resource group that you want to filter the VM's on by entering a value in the Value field. ((note that the Resource Group that you deployed your resources to is already prepopulated, if this is the same Resource Group whose VM's you want to Start/Stop then leave that else  you could remove the value and leave it blank as well)

* Tags:
Optionally, if you want to filter the VM's by Tags, by entering the appropriate value in the Tags Value field. This will  filter the VM's to only those that have that Tags defined on the VM (with any value).

Note that SubscriptionId is a required field, but both Resource Group and Tags are optional and can be both used or used individually.


![Image of LogicApps](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/Sub-RG-Tags.jpg)

Once done, save the Logic Apps. Finally go ahead and Enable the Logic Apps.

![Image of LogicApps](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/Enable.jpg)

Once enabled, the Logic Apps will trigger on the schedule. You can test out the Logic Apps by clicking on Run Trigger to test out if the functionality works. 

### Output and Troubleshooting

All the runs of the Logic Apps can be viewed in the Logic Apps output window

![Image of Results](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/RunsList.jpg)

Clicking on a Run will take you to the detailed view of the Run result. Clicking on the StartStopVMs activity right at the bottom and looking at the Body will give you a listing on the number of VM's affected and their names. This is typically where you can also find any errors that were encountered. 

![Image of Results](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/Output.jpg)

#### Common issues

* Not Authorized 

One of the most common errors that you might see is a "Not Authorized" error. 

![Image of Results](https://github.com/anirudhgarg/functions-logicapp-startstopvms/blob/master/StartStopVMs/images/NotAuthorized.jpg)

This is usually because you havent configured the Functions MSI to access the Subscription etc. 





