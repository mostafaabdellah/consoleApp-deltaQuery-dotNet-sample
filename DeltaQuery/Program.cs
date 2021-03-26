// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using DeltaQuery.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using static DeltaQuery.Resource;

namespace DeltaQuery
{
    public class DeltaLinks
    {
        public IDriveItemDeltaCollectionPage DeltaCollection { get; set; }
        public string DeltaLink { get; set; }
        public DateTime LastSyncDate { get; set; }
    }
    public class Resource
    {
        [Key]
        public Guid Id { get; set; }
        public string SiteUrl { get; set; }
        public string WebUrl { get; set; }
        public string SiteId { get; set; }
        public string ListId { get; set; }
        public string ListItemUniqueId { get; set; }
        public ResourceType ResType{ get; set; }
        public Activity ActType { get; set; }
        public DateTime OrgActionDate { get; set; }
        public DateTime ObsActionDate { get; set; }
        public int TimeDif { get; set; }
        public string Message { get; set; }
        public enum Activity
        {
            Added = 1,
            Renamed = 2,
            Moved = 3,
            Deleted = 4,
            VersionAdded = 6,
            Exist=7,
            Unknown=8
        }
        public enum ResourceType
        {
            Team = 1,
            Folder = 2,
            File = 3
        }
    }
    class Program
    {
        // The number of seconds to wait between delta queries
        private static int interval=5,processTime=0;
        private static DateTime lastProcessTime = DateTime.UtcNow;
        private static DateTime startTime = DateTime.UtcNow, endTime;
        private static bool firstCall = true;
        private static GraphServiceClient graphClient;
        private static IDictionary<string, DeltaLinks> teamSitesDeltaLinks = new Dictionary<string, DeltaLinks>();
        private static int teamsDeltaCalls, libraryDeltaCalls, activitiesCalls;
        private static IList<Resource> resources = new List<Resource>();
        private static bool showOnConsole=true;
        static async Task Main(string[] args)
        {
            var authProvider = new DeviceCodeAuthProvider();
            graphClient = new GraphServiceClient(authProvider);
            await WatchTeamsAsync();
            endTime = DateTime.UtcNow;
        }

        private static async Task WatchTeamsAsync()
        {
            var deltaCollection = await graphClient.Groups
                .Delta()
                .Request()
                .Select("id,displayName,visibility,resourceProvisioningOptions,CreatedDateTime")
                .GetAsync();
            teamsDeltaCalls++;
            while (true)
            {
                if (deltaCollection.CurrentPage.Count <= 0)
                {
                    //Console.WriteLine("No changes on teams...");
                    await WatchTeamsSitesAsync();
                }
                else
                {
                    var teamsFiltered = deltaCollection.CurrentPage.Where(w =>
                        w.ResourceProvisioningOptions != null
                    && w.ResourceProvisioningOptions.Contains("Team"));
                    if (firstCall)
                        foreach (var team in teamsFiltered)
                        {
                            //AddTeam(team, Activity.Exist);
                            if (!teamSitesDeltaLinks.ContainsKey(team.Id)) teamSitesDeltaLinks.Add(team.Id, null);
                        }
                    else
                        foreach (var team in teamsFiltered)
                        {
                            LogAddedTeam(team, Activity.Added);
                            if (!teamSitesDeltaLinks.ContainsKey(team.Id)) teamSitesDeltaLinks.Add(team.Id, null);
                        }
                }

                var nextLink = string.Empty;
                var deltaLink = string.Empty;

                if (deltaCollection.AdditionalData.ContainsKey("@odata.nextLink") 
                    && deltaCollection.AdditionalData["@odata.nextLink"] != null)
                {
                    nextLink = deltaCollection.AdditionalData["@odata.nextLink"].ToString();
                    deltaCollection.InitializeNextPageRequest(graphClient, nextLink);
                    deltaCollection = await deltaCollection.NextPageRequest
                        .GetAsync();
                    teamsDeltaCalls++;
                    continue;
                }
                
                if (deltaCollection.AdditionalData["@odata.deltaLink"] != null)
                    deltaLink = deltaCollection.AdditionalData["@odata.deltaLink"].ToString();

                await WatchTeamsSitesAsync();
                processTime = (int)DateTime.UtcNow.Subtract(lastProcessTime).TotalSeconds;
                lastProcessTime = DateTime.UtcNow;
                var wait = interval - processTime;
                if (wait < 0) wait = 0;
                await Task.Delay(wait * 1000);
                deltaCollection.InitializeNextPageRequest(graphClient, deltaLink);
                deltaCollection = await deltaCollection.NextPageRequest
                    .GetAsync();
                teamsDeltaCalls++;
                await UpdateDbAsync();
                firstCall = false;
            }

        }
        private static async Task UpdateDbAsync()
        {
            using (var context = new SyncDbContext())
            {
                context.Resources.AddRange(resources);
                await context.SaveChangesAsync();
                resources= resources = new List<Resource>();
            }
        }
        private static void LogAddedTeam(Group team, Activity type)
        {
            var record = new Resource()
            {
                ActType = type,
                OrgActionDate = team.CreatedDateTime.Value.UtcDateTime,
                ObsActionDate = DateTime.UtcNow,
                ResType = ResourceType.Team,
                TimeDif = (int)DateTime.UtcNow.Subtract(team.CreatedDateTime.Value.UtcDateTime).TotalSeconds,
                Message = $"New Team Created \"{team.DisplayName}\" On {team.CreatedDateTime} Visibility = {team.Visibility}"
            };
            
            //if (type == Activity.Exist)
            //    record.Message=$"Exist Team \"{team.DisplayName}\" Created On {team.CreatedDateTime} Visibility = {team.Visibility}";
            //else
            //    record.Message = $"New Team Created \"{team.DisplayName}\" On {team.CreatedDateTime} Visibility = {team.Visibility}";
            
            resources.Add(record);
            if(showOnConsole)
                Console.WriteLine(record.Message);
            Task.Delay(1 * 1000);
        }

        private static async Task WatchTeamsSitesAsync()
        {
            ICollection<string> keys = teamSitesDeltaLinks.Keys.ToList();
            foreach (var key in keys)
            {
                var pair = new KeyValuePair<string, DeltaLinks>(key, teamSitesDeltaLinks[key]);
               await WatchTeamSiteAsync(pair);
            }
        }

        private static async Task WatchTeamSiteAsync(KeyValuePair<string, DeltaLinks> pair)
        {
            try
            {
                IDriveItemDeltaCollectionPage deltaCollection;
                var deltaLinks = new DeltaLinks
                {
                };

                if (pair.Value == null)
                {
                    deltaCollection = await graphClient.Groups[pair.Key].Drive.Root
                        .Delta()
                        .Request()
                        .Select("Shared,ETag,CTag,CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                        .GetAsync();
                    deltaLinks.DeltaCollection = deltaCollection;
                    deltaLinks.LastSyncDate = DateTime.UtcNow;
                    libraryDeltaCalls++;
                }
                else
                {
                    deltaLinks.LastSyncDate = pair.Value.LastSyncDate;
                    deltaCollection = pair.Value.DeltaCollection;
                    deltaCollection.InitializeNextPageRequest(graphClient, pair.Value.DeltaLink);
                    deltaCollection = await deltaCollection.NextPageRequest
                        .GetAsync();
                    libraryDeltaCalls++;
                }

                if (deltaCollection.CurrentPage.Count > 0)
                {
                    if (!firstCall)
                    foreach (var drive in deltaCollection.CurrentPage)
                        await ProcessChangesAsync(drive, deltaLinks.LastSyncDate);
                    //else
                    //    foreach (var item in deltaCollection.CurrentPage)
                    //        Console.WriteLine($"{item.WebUrl}");
                }



                if (deltaCollection.AdditionalData.ContainsKey("@odata.nextLink") && deltaCollection.AdditionalData["@odata.nextLink"] != null)
                {
                    deltaLinks.DeltaLink = deltaCollection.AdditionalData["@odata.nextLink"].ToString();
                    deltaLinks.DeltaCollection = deltaCollection;
                    pair = new KeyValuePair<string, DeltaLinks>(pair.Key, deltaLinks);
                    await WatchTeamSiteAsync(pair);
                }
                else if (deltaCollection.AdditionalData["@odata.deltaLink"] != null)
                {
                    deltaLinks.DeltaLink = deltaCollection.AdditionalData["@odata.deltaLink"].ToString();
                }
                deltaLinks.DeltaCollection = deltaCollection;
                deltaLinks.LastSyncDate = DateTime.UtcNow;
                teamSitesDeltaLinks[pair.Key] = deltaLinks;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error WatchTeamSiteAsync site {pair.Key}: {exception.Message}");
            }
        }
        private static async Task ProcessChangesAsync(DriveItem drive, DateTime lastSyncDate)
        {
            try
            {
                if (drive.Root != null)
                    return;

                if (SkipFolder(drive,lastSyncDate))
                    return;

                if (FolderDeleted(drive))
                    LogResourceDeleted(drive,ResourceType.Folder);
                else if (FileDeleted(drive))
                    LogResourceDeleted(drive, ResourceType.File);
                else if (NewFolder(drive,lastSyncDate))
                    LogResourceAdded(drive, ResourceType.Folder);
                else if (FolderChanged(drive,lastSyncDate))
                    await LogResourceActivities(drive, ResourceType.Folder,lastSyncDate);
                else if (NewFile(drive,lastSyncDate))
                    LogResourceAdded(drive, ResourceType.File);
                else if (FileChanged(drive,lastSyncDate))
                    await LogResourceActivities(drive,ResourceType.File,lastSyncDate);
                //else
                //    await GetItemDetails(drive);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error Process Changes {drive.WebUrl}: {exception.Message}");
            }
        }

        private static void LogResourceDeleted(DriveItem drive, ResourceType resType)
        {
            var record = new Resource()
            {
                ResType = resType,
                ActType = Activity.Deleted,
                ListId=drive.SharepointIds.ListId,
                ListItemUniqueId=drive.SharepointIds.ListItemUniqueId,
                SiteId=drive.SharepointIds.SiteId,
                SiteUrl=drive.SharepointIds.SiteUrl,
                WebUrl=drive.WebUrl,
                OrgActionDate=drive.LastModifiedDateTime.Value.UtcDateTime,
                ObsActionDate = DateTime.UtcNow,
                TimeDif = (int)DateTime.UtcNow.Subtract(drive.CreatedDateTime.Value.UtcDateTime).TotalSeconds,
                Message = $"{resType} Deleted {drive.SharepointIds.SiteUrl} ListId={drive.SharepointIds.ListId} Id={drive.SharepointIds.ListItemUniqueId}"
            };

            resources.Add(record);
            if (showOnConsole)
                Console.WriteLine(record.Message);
        }
        private static void LogResourceAdded(DriveItem drive, ResourceType resType)
        {
            var record = new Resource()
            {
                ResType = resType,
                ActType = Activity.Added,
                ListId = drive.SharepointIds.ListId,
                ListItemUniqueId = drive.SharepointIds.ListItemUniqueId,
                SiteId = drive.SharepointIds.SiteId,
                SiteUrl = drive.SharepointIds.SiteUrl,
                WebUrl = drive.WebUrl,
                OrgActionDate=drive.CreatedDateTime.Value.UtcDateTime,
                ObsActionDate=DateTime.UtcNow,
                TimeDif = (int)DateTime.UtcNow.Subtract(drive.CreatedDateTime.Value.UtcDateTime).TotalSeconds,
                Message = $"New {resType} Created {drive.WebUrl}"
            };

            resources.Add(record);
            if (showOnConsole)
                Console.WriteLine(record.Message);
        }
        private static void LogResourceChanged(DriveItem drive, ResourceType resType,Activity activity, ItemActivityOLD act)
        {
            var record = new Resource()
            {
                ResType = resType,
                ActType = activity,
                ListId = drive.SharepointIds.ListId,
                ListItemUniqueId = drive.SharepointIds.ListItemUniqueId,
                SiteId = drive.SharepointIds.SiteId,
                SiteUrl = drive.SharepointIds.SiteUrl,
                WebUrl = drive.WebUrl,
                OrgActionDate = drive.CreatedDateTime.Value.UtcDateTime,
                ObsActionDate = DateTime.UtcNow,
                TimeDif = (int)DateTime.UtcNow.Subtract(drive.CreatedDateTime.Value.UtcDateTime).TotalSeconds,
                Message = $"{resType} {activity} {drive.WebUrl}"
            };

            if(activity==Activity.Renamed)
                record.Message = $"{resType} {activity} from {act.Action.Rename.OldName} to {drive.WebUrl}";
            else if(activity==Activity.Moved)
                record.Message = $"{resType} {activity} from {act.Action.Move.From} to {drive.WebUrl }";
            else if (activity == Activity.VersionAdded)
                record.Message = $"New Version {act.Action?.Version?.NewVersion} Added to {drive.WebUrl }";
            else
                record.Message = $"{activity} change on {resType} {drive.WebUrl}";


            resources.Add(record);
            if (showOnConsole)
                Console.WriteLine(record.Message);
        }

        private static async Task LogResourceActivities(DriveItem drive, ResourceType resType, DateTime lastSyncDate)
        {
            var spIds = drive.SharepointIds;

            var collection = await graphClient
                .Sites[spIds.SiteId]
                .Lists[spIds.ListId]
                .Items[spIds.ListItemId].Activities
                .Request()
                .Top(1)
                .Select("action,times")
                .GetAsync();

            activitiesCalls++;
            var activities = collection.Where(w => w.Times.RecordedDateTime.Value.CompareTo(lastSyncDate) > 0);

            foreach (var act in activities)
            {
                if (act.Action.Rename != null)
                    LogResourceChanged(drive, resType, Activity.Renamed,act);
                else if (act.Action.Move != null)
                    LogResourceChanged(drive, resType, Activity.Moved, act);
                else if (act.Action.Edit != null)
                    LogResourceChanged(drive, resType, Activity.VersionAdded, act);
                else
                    LogResourceChanged(drive, resType, Activity.Unknown, act);
            }
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

        private static bool FileChanged(DriveItem drive, DateTime lastSyncDate)
        {
            return drive.File != null
                            && drive.CreatedDateTime != drive.LastModifiedDateTime
                            && lastSyncDate.CompareTo(drive.LastModifiedDateTime.Value.DateTime) <= 0;
        }

        private static bool SkipFolder(DriveItem drive, DateTime lastSyncDate)
        {
            return drive.Folder != null
                && drive.Deleted==null
                            && lastSyncDate.CompareTo(drive.LastModifiedDateTime.Value.DateTime) >= 0;
        }
        private static bool NewFile(DriveItem drive, DateTime lastSyncDate)
        {
            return drive.File != null
                            && drive.CreatedDateTime == drive.LastModifiedDateTime
                            && lastSyncDate.CompareTo(drive.LastModifiedDateTime.Value.DateTime) <= 0;
        }
        private static bool FolderChanged(DriveItem drive, DateTime lastSyncDate)
        {
            return drive.Folder != null
                            && drive.Size == 0
                            && drive.CreatedDateTime != drive.LastModifiedDateTime
                            && lastSyncDate.CompareTo(drive.LastModifiedDateTime.Value.DateTime) <= 0;
        }

        private static bool NewFolder(DriveItem drive, DateTime lastSyncDate)
        {
            return drive.Folder != null
                            && drive.Size == 0
                            && drive.CreatedDateTime == drive.LastModifiedDateTime
                            && lastSyncDate.CompareTo(drive.LastModifiedDateTime.Value.DateTime) <= 0;
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
            var collections = await graphClient.Sites
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
            var deltaCollection = await graphClient.Sites
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

                    deltaCollection.InitializeNextPageRequest(graphClient, deltaLink.ToString());
                    deltaCollection = await deltaCollection.NextPageRequest.GetAsync();
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
