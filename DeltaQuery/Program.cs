// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using DeltaQuery.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeltaQuery
{
    class Program
    {
        // The number of seconds to wait between delta queries
        private static int pollInterval=1;
        private static DateTime lastSync = DateTime.MinValue.Add(new TimeSpan(0,0,pollInterval));
        private static bool firstCall = true;
        // Graph client
        private static GraphServiceClient _graphClient;

        // In-memory "database" of mail folders
        private static List<MailFolder> _localMailFolders = new List<MailFolder>();

        static async Task Main(string[] args)
        {
            var authProvider = new DeviceCodeAuthProvider();
            _graphClient = new GraphServiceClient(authProvider);

            //await GetSitecollections();

            var deltaCollection = await _graphClient.Sites["afd504d1-b33f-40e1-a875-3c5b3da49ead"].Drive.Root
                .Delta()
                .Request()
                .Select("CreatedDateTime,Deleted,File,Folder,Id,LastModifiedDateTime,Name,Root,SharepointIds,Size,WebUrl")
                //.Header("Prefer", "return=minimal")
                .GetAsync();
            await WatchQueryAsync(deltaCollection);
            //var deltaOneDrive= await _graphClient.Users["ebcd42aa-77cc-4533-9c04-ba32df03c761"] .Drive.Root
            //    .Delta()
            //    .Request()
            //    .Header("Prefer", "return=minimal")
            //    //.Header("ocp-aad-dq-include-only-changed-properties", "true")
            //    .GetAsync();
            //await WatchQueryAsync(deltaOneDrive);
        }

        private static async Task WatchQueryAsync(IDriveItemDeltaCollectionPage deltaCollection)
        {
            
            while (true)
            {
                if (deltaCollection.CurrentPage.Count <= 0)
                {
                    //Console.WriteLine("No changes...");
                }
                else
                {
                    bool morePagesAvailable;
                    do
                    {
                        // If there is a NextPageRequest, there are more pages
                        morePagesAvailable = deltaCollection.NextPageRequest != null;
                        if (firstCall)
                            foreach (var item in deltaCollection.CurrentPage)
                                Console.WriteLine($"{item.WebUrl}");
                        else
                            foreach (var drive in deltaCollection.CurrentPage)
                                await ProcessChangesAsync(drive);

                        if (morePagesAvailable)
                        {
                            // Get the next page of results
                            deltaCollection = await deltaCollection.NextPageRequest.GetAsync();
                        }
                    }
                    while (morePagesAvailable);
                }

                // Once we've iterated through all of the pages, there should
                // be a delta link, which is used to request all changes since our last query
                var deltaLink = string.Empty;
                if (deltaCollection.AdditionalData.ContainsKey("@odata.nextLink") && deltaCollection.AdditionalData["@odata.nextLink"] != null)
                    deltaLink = deltaCollection.AdditionalData["@odata.nextLink"].ToString();
                else if (deltaCollection.AdditionalData["@odata.deltaLink"] != null)
                    deltaLink = deltaCollection.AdditionalData["@odata.deltaLink"].ToString();
                if (!string.IsNullOrEmpty(deltaLink))
                {
                    lastSync = DateTime.Now.ToUniversalTime();
                    firstCall = false;
                    //Console.WriteLine($"Processed current delta. Will check back in {pollInterval} seconds.");
                    await Task.Delay(pollInterval * 1000);
                    deltaCollection.InitializeNextPageRequest(_graphClient, deltaLink);
                    deltaCollection = await deltaCollection.NextPageRequest
                        //.Header("Prefer", "return=minimal")
                        //.Header("ocp-aad-dq-include-only-changed-properties", "true")
                        .GetAsync();
                }
            }

        }
        private static async Task ProcessChangesAsync(DriveItem drive)
        {
            if (drive.Root != null)
                return;

            if (SkipFolder(drive))
                return;

            if (FolderDeleted(drive))
                Console.WriteLine($"\n Folder Deleted {drive.SharepointIds.SiteUrl} ListId={drive.SharepointIds.ListId} Id={drive.SharepointIds.ListItemId}\n");
            else if (FileDeleted(drive))
                Console.WriteLine($"\n File Deleted {drive.SharepointIds.SiteUrl} ListId={drive.SharepointIds.ListId} Id={drive.SharepointIds.ListItemId}\n");
            else if (NewFolder(drive))
                Console.WriteLine($"\n New Folder Created {drive.WebUrl}\n");
            else if (FolderChanged(drive))
            {
                var itemDeatils = await GetItemDetails(drive);
                Console.WriteLine($"\n Folder Changed {drive.WebUrl}\n");
            }
            else if (NewFile(drive))
                Console.WriteLine($"\n New File Created {drive.WebUrl}\n");
            else if (FileChanged(drive))
            {
                var itemDeatils = await GetItemDetails(drive);
                Console.WriteLine($"\n File Changed {drive.WebUrl}\n");
            }


        }

        private static async Task<ListItem> GetItemDetails(DriveItem drive)
        {
            var spIds = drive.SharepointIds;
            var activities=await _graphClient
                .Sites[spIds.SiteId]
                .Lists[spIds.ListId]
                .Items[spIds.ListItemUniqueId]
                .Request()
                .GetAsync();

            return await _graphClient
                .Sites[spIds.SiteId]
                .Lists[spIds.ListId]
                .Items[spIds.ListItemUniqueId]
                //.GetActivitiesByInterval()
                .Request()
                .GetAsync();
        }

        private static bool FileDeleted(DriveItem drive)
        {
            return drive.File != null
                && drive.Deleted != null;
        }

        private static bool FolderDeleted(DriveItem drive)
        {
            return drive.Folder != null
                && drive.Deleted != null;
        }

        private static bool FileChanged(DriveItem drive)
        {
            return drive.File != null
                            && !firstCall
                            && drive.CreatedDateTime != drive.LastModifiedDateTime
                            && lastSync.Subtract(new TimeSpan(0, 0, pollInterval)).CompareTo(drive.LastModifiedDateTime.Value.DateTime) < 0;
        }

        private static bool SkipFolder(DriveItem drive)
        {
            return drive.Folder != null
                && drive.Deleted==null
                            && lastSync.Subtract(new TimeSpan(0, 0, pollInterval)).CompareTo(drive.LastModifiedDateTime.Value.DateTime) > 0;
        }
        private static bool NewFile(DriveItem drive)
        {
            return drive.File != null
                            && !firstCall
                            && drive.CreatedDateTime == drive.LastModifiedDateTime
                            && lastSync.Subtract(new TimeSpan(0, 0, pollInterval)).CompareTo(drive.LastModifiedDateTime.Value.DateTime) < 0;
        }
        private static bool FolderChanged(DriveItem drive)
        {
            return drive.Folder != null
                            && !firstCall
                            && drive.Size == 0
                            && drive.CreatedDateTime != drive.LastModifiedDateTime
                            && lastSync.Subtract(new TimeSpan(0, 0, pollInterval)).CompareTo(drive.LastModifiedDateTime.Value.DateTime) < 0;
        }

        private static bool NewFolder(DriveItem drive)
        {
            return drive.Folder != null
                            && !firstCall
                            && drive.Size == 0
                            && drive.CreatedDateTime == drive.LastModifiedDateTime
                            && lastSync.Subtract(new TimeSpan(0, 0, pollInterval)).CompareTo(drive.LastModifiedDateTime.Value.DateTime) < 0;
        }

        private static void DisplayDriveProp(DriveItem drive)
        {
            Console.WriteLine(drive.WebUrl);
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (var prop in drive.GetType().GetProperties())
            {
                var value = drive.GetType().GetProperty(prop.Name).GetValue(drive, null)?.ToString();
                if (!string.IsNullOrEmpty(value))
                    Console.WriteLine($"{prop.Name} = {value}");
            }
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        static async Task GetSitecollections()
        {
            var collections = await _graphClient.Sites
    .Request()
    //.Select("siteCollection,webUrl")
    .GetAsync();
            Console.WriteLine(collections);
            foreach (var item in collections)
            {
                Console.WriteLine($"WebUrl= {item.WebUrl}, SiteId={item.Id}");
            }
        }
            static async Task WatchMailFolders(int pollInterval)
        {
            // Get first page of mail folders
            //IMailFolderDeltaCollectionPage deltaCollection;
            var deltaCollection = await _graphClient.Sites
    .Request()
    //.Filter("siteCollection/root ne null")
    .Select("siteCollection,webUrl")
    .GetAsync();


            while (true)
            {
                if (deltaCollection.CurrentPage.Count <= 0)
                {
                    Console.WriteLine("No changes...");
                }
                else
                {
                    bool morePagesAvailable = false;
                    do
                    {
                        // If there is a NextPageRequest, there are more pages
                        morePagesAvailable = deltaCollection.NextPageRequest != null;
                        foreach(var mailFolder in deltaCollection.CurrentPage)
                        {
                            //await ProcessChanges(mailFolder);
                        }

                        if (morePagesAvailable)
                        {
                            // Get the next page of results
                            deltaCollection = await deltaCollection.NextPageRequest.GetAsync();
                        }
                    }
                    while (morePagesAvailable);
                }

                // Once we've iterated through all of the pages, there should
                // be a delta link, which is used to request all changes since our last query
                var deltaLink = deltaCollection.AdditionalData["@odata.deltaLink"];
                if (!string.IsNullOrEmpty(deltaLink.ToString()))
                {
                    Console.WriteLine($"Processed current delta. Will check back in {pollInterval} seconds.");
                    await Task.Delay(pollInterval * 1000);

                    deltaCollection.InitializeNextPageRequest(_graphClient, deltaLink.ToString());
                    deltaCollection = await deltaCollection.NextPageRequest.GetAsync();
                }
            }
        }

        static async Task ProcessChanges(MailFolder mailFolder)
        {
            // Check if the local list of folders already contains this one
            var localFolder = _localMailFolders.Find(f => f.Id == mailFolder.Id);

            bool isDeleted = mailFolder.AdditionalData != null ?
                mailFolder.AdditionalData.ContainsKey("@removed") :
                false;

            if (localFolder != null)
            {
                // In this case it's a delete or an update of a folder
                // we already know about
                if (isDeleted)
                {
                    // Remove the entry from the local list
                    Console.WriteLine($"Folder {localFolder.DisplayName} deleted");
                    _localMailFolders.Remove(localFolder);
                }
                else
                {
                    Console.WriteLine($"Folder {localFolder.DisplayName} updated:");

                    // Was it renamed?
                    if (string.Compare(localFolder.DisplayName, mailFolder.DisplayName) != 0)
                    {
                        Console.WriteLine($"  - Renamed to {mailFolder.DisplayName}");
                    }

                    // Was it moved?
                    if (string.Compare(localFolder.ParentFolderId, mailFolder.ParentFolderId) != 0)
                    {
                        // Get the parent folder
                        var parent = await _graphClient.Me
                            .MailFolders[mailFolder.ParentFolderId]
                            .Request()
                            .GetAsync();

                        Console.WriteLine($"  - Moved to {parent.DisplayName} folder");
                    }

                    // Remove old entry and add new one
                    _localMailFolders.Remove(localFolder);
                    _localMailFolders.Add(mailFolder);
                }
            }
            else
            {
                // No local match
                if (isDeleted)
                {
                    // Folder deleted, but we never knew about it anyway
                    Console.WriteLine($"Unknown folder with ID {mailFolder.Id} deleted");
                }
                else
                {
                    // New folder, add to local list
                    Console.WriteLine($"Folder {mailFolder.DisplayName} added");
                    _localMailFolders.Add(mailFolder);
                }
            }
        }

        static IConfigurationRoot LoadAppSettings()
        {
            // Load the values stored in the secret
            // manager
            var appConfig = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            // Check for required settings
            if (string.IsNullOrEmpty(appConfig["AzureAppId"]))
            {
                return null;
            }

            return appConfig;
        }
    }
}
