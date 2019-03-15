# ConsoleApp-MicrosoftGraphAPI-DeltaQuery-DotNet

This console application demonstrates how to make Delta Query calls to the Graph API, allowing applications to request only changed data from Microsoft Graph tenants.

The sample uses demonstates how graph calls can be made with the Graph SDK and how the reponses can be handled.

The specific example used in this sample involves monitoring changes(addition and removal) of MailFolders in an individual's email account.

## How To Run This Sample

To run this sample you will need:
- Visual Studio 2017
- An Internet connection
- An Azure Active Directory (Azure AD) tenant. For more information on how to get an Azure AD tenant, please see [How to get an Azure AD tenant](https://azure.microsoft.com/en-us/documentation/articles/active-directory-howto-tenant/) 

### Step 1:  Clone or download this repository

From your shell or command line:

`git clone https://github.com/microsoftgraph/ConsoleApp-DeltaQuery-DotNet`

### Step 2:  Register the sample application with your Azure Active Directory tenant

There is one project in this sample. To register it, you can:

- either follow the steps [Step 2: Register the sample with your Azure Active Directory tenant](#step-2-register-the-sample-with-your-azure-active-directory-tenant) and [Step 3:  Configure the sample to use your Azure AD tenant](#choose-the-azure-ad-tenant-where-you-want-to-create-your-applications)
- or use PowerShell scripts that:
  - **automatically** creates the Azure AD applications and related objects (passwords, permissions, dependencies) for you
  - modify the Visual Studio projects' configuration files.

If you want to use this automation:

1. On Windows run PowerShell and navigate to the root of the cloned directory
1. In PowerShell run:

   ```PowerShell
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force
   ```

1. Run the script to create your Azure AD application and configure the code of the sample application accordinly.

   ```PowerShell
   .\AppCreationScripts\Configure.ps1
   ```

   > Other ways of running the scripts are described in [App Creation Scripts](./AppCreationScripts/AppCreationScripts.md)

1. Open the Visual Studio solution and click start

If you don't want to use this automation, follow the steps below

#### Choose the Azure AD tenant where you want to create your applications

As a first step you'll need to:

1. Sign in to the [Azure portal](https://portal.azure.com) using either a work or school account or a personal Microsoft account.
1. If your account gives you access to more than one tenant, select your account in the top right corner, and set your portal session to the desired Azure AD tenant
   (using **Switch Directory**).
1. In the left-hand navigation pane, select the **Azure Active Directory** service, and then select **App registrations (Preview)**.

#### Register the client app (ConsoleApp-DeltaQuery-DotNet)

1. In **App registrations (Preview)** page, select **Register an Application**.
1. When the **Register an application page** appears, enter your application's registration information:
   - In the **Name** section, enter a meaningful application name that will be displayed to users of the app, for example `ConsoleApp-DeltaQuery-DotNet`.
   - In the **Supported account types** section, select **Accounts in any organizational directory and personal Microsoft accounts (e.g. Skype, Xbox, Outlook.com)**.
   - Select **Register** to create the application.
1. On the app **Overview** page, find the **Application (client) ID** value and record it for later. You'll need it to configure the Visual Studio configuration file for this project.
1. In the list of pages for the app, select **Authentication**
   - In the *Suggested Redirect URIs for public clients(mobile,desktop)*, check the second box so that the app can work with the MSAL libs used in the application. (The box should contain the option *urn:ietf:wg:oauth:2.0:oob*). 
1. In the list of pages for the app, select **API permissions**
   - Click the **Add a permission** button and then,
   - Ensure that the **Microsoft APIs** tab is selected
   - In the *Commonly used Microsoft APIs* section, click on **Microsoft Graph**
   - In the **Delegated permissions** section, ensure that the right permissions are checked: **Mail.Read**. Use the search box if necessary.
   - Select the **Add permissions** button

### Step 3:  Configure the sample to use your Azure AD tenant

In the steps below, "ClientID" is the same as "Application ID" or "AppId".

Open the solution in Visual Studio to configure the projects

#### Configure the client project

1. In the *ConsoleApplication* folder, rename the `appsettings.json.example` file to `appsettings.json`
1. Open and edit the `appsettings.json` file to make the following change
    1. Find the line where `ClientId` is set as `YOUR_CLIENT_ID_HERE` and replace the existing value with the application ID (clientId) of the `ConsoleApp-DeltaQuery-DotNet` application copied from the Azure portal.

Clean the solution, rebuild the solution, and start it in the debugger.

## Contributing

If you'd like to contribute to this sample, see [CONTRIBUTING.MD](/CONTRIBUTING.md).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Questions and comments

We'd love to get your feedback about the Microsoft Graph Webhooks sample using WebJobs SDK. You can send your questions and suggestions to us in the [Issues](https://github.com/microsoftgraph/ConsoleApp-DeltaQuery-DotNet/issues) section of this repository.

Questions about Microsoft Graph in general should be posted to [Stack Overflow](https://stackoverflow.com/questions/tagged/MicrosoftGraph). Make sure that your questions or comments are tagged with *[MicrosoftGraph]*.

If you have a feature suggestion, please post your idea on our [User Voice](https://officespdev.uservoice.com/) page, and vote for your suggestions there.

## Additional resources

- [AAD DQ sample](https://github.com/Azure-Samples/active-directory-dotnet-graphapi-diffquery)
- [Working with Delta Query in Microsoft Graph](https://developer.microsoft.com/en-us/graph/docs/concepts/delta_query_overview)
- [Microsoft Graph developer site](https://developer.microsoft.com/en-us/graph/)
- [Call Microsoft Graph in an ASP.NET MVC app](https://developer.microsoft.com/en-us/graph/docs/platform/aspnetmvc)
- [MSAL.NET](https://aka.ms/msal-net)

Copyright (c) 2019 Microsoft Corporation. All rights reserved.