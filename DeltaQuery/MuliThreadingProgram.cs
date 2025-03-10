﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using DeltaQuery.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static DeltaQuery.Resource;

namespace DeltaQuery
{

    class MuliThreadingProgram
    {
        // The number of seconds to wait between delta queries
        private static int interval=100,processTime=0;
        private static DateTime lastProcessTime = DateTime.UtcNow;
        private static DateTime startTime = DateTime.UtcNow, endTime;
        private static bool firstCall = true;
        private static GraphServiceClient graphClient;
        private static IDictionary<string, DeltaLinks> teamSitesDeltaLinks = new Dictionary<string, DeltaLinks>();
        private static int teamsDeltaCalls, libraryDeltaCalls, activitiesCalls;
        private static IList<Resource> resources = new List<Resource>();
        private static bool showOnConsole=false;
        private static bool noChanges = false;
        private static int iterationCounter=0;
        private static int MaxIteration = 1;
        private static Performance perf = new Performance();
        private static long totalDuration = 0;
        private static int noOfRuns = 0;
        private static List<TeamTable> allTeams = new List<TeamTable>();
        private static bool multiThreading=true;
        private static int noOfThreads = 8;
        private static int noTeams = 1000;
        static async Task MainMT(string[] args)
        {
            var authProvider = new DeviceCodeAuthProvider();
            //graphClient = new GraphServiceClient(authProvider);

            using HttpClient client = new HttpClient(new HttpClientHandler() { MaxConnectionsPerServer = 1440 });
            graphClient = new GraphServiceClient(client);
            graphClient.AuthenticationProvider = authProvider;
            await DbOperations.ClearResourcesAsync();
            Console.WriteLine("Clear DB ..");
            Console.WriteLine("Start Watching..");
            perf.StartOn = DateTime.UtcNow;
            perf.TeamsCount = noTeams;
            //await RunBatchAsync();
            await BatchRequestExample(graphClient,20);
            //await WatchTeamsAsync(noTeams);
            perf.ActivitiesCalls = activitiesCalls;
            perf.DeltaCalls = libraryDeltaCalls;
            perf.Duration = (int)DateTime.UtcNow.Subtract(perf.StartOn).TotalSeconds;
            perf.CompletedOn = DateTime.UtcNow;
            perf.AverageSyncDuration = DbOperations.GetAverageSync();
            perf.TotalDuration = totalDuration;
            perf.NoOfRuns = noOfRuns;
            perf.AvgDuration = totalDuration / noOfRuns;
            await DbOperations.UpdatePerformanceAsync(perf);
            Console.WriteLine($"Teams={perf.TeamsCount} - DeltaCalls={perf.DeltaCalls} - ActivitiesCalls={perf.ActivitiesCalls} - AverageSyncDuration={perf.AverageSyncDuration}");
        }

        private static async Task RunBatchAsync()
        {
            var teams = DbOperations.GetTeams(5);
            var batch = new BatchRequestContent();
            //foreach (var team in teams)
            {
                var deltaRequest01 = graphClient.Groups[teams[0].TeamId].Drive.Root
                            .Delta()
                            .Request()
                            .GetAsync().Result;

                var deltaRequest0 = graphClient.Groups[teams[0].TeamId].Drive.Root
                            .Delta()
                            .Request()
                            //.Select("CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                            ;
                var deltaRequest1 = graphClient.Groups[teams[0].TeamId].Drive.Root
                            .Delta()
                            .Request()
                            //.Select("CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                            ;
                var deltaResp0=batch.AddBatchRequestStep(deltaRequest0);
                var deltaResp1 = batch.AddBatchRequestStep(deltaRequest1);
                var returnedResponse = await graphClient.Batch.Request().PostAsync(batch);

                try
                {
                    var deltaResponse0 = await returnedResponse.GetResponseByIdAsync<IDriveItemDeltaCollectionPage>(deltaResp0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                try
                {
                    var deltaResponse1 = await returnedResponse.GetResponseByIdAsync<IDriveItemDeltaCollectionPage>(deltaResp1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


            }
        }
        public async static Task BatchRequestExample(GraphServiceClient graphServiceClient, int limit)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            var batchRequestContent = new BatchRequestContent();
            var teams = DbOperations.GetTeams(limit);

            // 1. construct a Batch request 
            for (int i = 0; i < teams.Count; i++)
            {
                var requestUrl1 = graphServiceClient.Groups[teams[i].TeamId].Drive.Root
                                .Delta()
                                .Request().RequestUrl;
                var request1 = new HttpRequestMessage(HttpMethod.Get, requestUrl1);
                var requestStep1 = new BatchRequestStep($"{i}", request1, null);
                batchRequestContent.AddBatchRequestStep(requestStep1);
            }
            
            //3. Submit request
            var batchRequest = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/$batch");
            batchRequest.Content = batchRequestContent;
            await graphServiceClient.AuthenticationProvider.AuthenticateRequestAsync(batchRequest);
            var httpClient = new HttpClient();
            var batchResponse = await httpClient.SendAsync(batchRequest);

            // 3. Process response
            var batchResponseContent = new BatchResponseContent(batchResponse);
            var responses = await batchResponseContent.GetResponsesAsync();
            foreach (var response in responses)
            {
                if (response.Value.IsSuccessStatusCode)
                {
                    //Console.WriteLine();
                    //Console.WriteLine($"response {response.Key} - {await response.Value.Content.ReadAsStringAsync()}");
                    //Console.WriteLine();
                    Console.WriteLine($"response {response.Key}");
                }
            }
            watch.Stop();
            Console.WriteLine($"Checking Teams completed on {watch.ElapsedMilliseconds / 1000} seconds");

        }
        private static async Task WatchTeamsAsync(int limit)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            //await GetTeams();
            //await DbOperations.AddTeamsToTable(allTeams);

            var teams = DbOperations.GetTeams(limit);//.Skip(limit*2);//allTeams.OrderBy(o => o.CreatedDateTime).Take(limit);

            foreach (var team in teams)
                teamSitesDeltaLinks.Add(team.TeamId, null);

            watch.Stop();
            Console.WriteLine($"Checking Teams completed on {watch.ElapsedMilliseconds / 1000} seconds");

            while (!noChanges || iterationCounter != MaxIteration)
            {
                try
                {
                    await WatchTeamsSitesAsync();
                    processTime = (int)DateTime.UtcNow.Subtract(lastProcessTime).TotalSeconds;
                    lastProcessTime = DateTime.UtcNow;
                    var wait = interval - processTime;
                    if (wait < 0) wait = 0;
                    await Task.Delay(wait * 1000);
                    await DbOperations.UpdateResourcesAsync(resources);
                    resources.Clear();
                    firstCall = false;

                    if (teamSitesDeltaLinks.Any(w => w.Value == null))
                    {
                        noChanges = false;
                        iterationCounter = 0;
                    }
                    else if (teamSitesDeltaLinks.All(w => w.Value.NoChanges))
                    {
                        noChanges = true;
                        ++iterationCounter;
                    }
                    else
                    {
                        noChanges = false;
                        iterationCounter = 0;
                    }
                    perf.ActivitiesCalls = activitiesCalls;
                    perf.DeltaCalls = libraryDeltaCalls;
                    perf.AverageSyncDuration = DbOperations.GetAverageSync();
                    await DbOperations.UpdatePerformanceAsync(perf);
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.Message);
                }
            }
        }

        private static async Task GetTeams()
        {

            var deltaCollection = await graphClient.Groups
                               .Request()
                               .Filter("resourceProvisioningOptions/Any(x:x eq 'Team')")
                               .Select("id,displayName,CreatedDateTime")
                               .GetAsync();
            AddTeamsToTable(deltaCollection);

            
            while (deltaCollection.AdditionalData.ContainsKey("@odata.nextLink")
                    && deltaCollection.AdditionalData["@odata.nextLink"] != null)
            {
                var nextLink = deltaCollection.AdditionalData["@odata.nextLink"].ToString();
                deltaCollection.InitializeNextPageRequest(graphClient, nextLink);
                deltaCollection = await deltaCollection.NextPageRequest
                    .GetAsync();
                AddTeamsToTable(deltaCollection);
            }

        }

        private static void AddTeamsToTable(IGraphServiceGroupsCollectionPage deltaCollection)
        {
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            Parallel.ForEach(deltaCollection, options, async team =>
            {
                await AddTeamToTable(team);
            });
        }

        private static async Task AddTeamToTable(Group team)
        {
            try
            {
                var teamSite = await graphClient.Groups[team.Id].Drive.Root
                                        .Request()
                                        .Select("CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                                        .GetAsync();
                allTeams.Add(new TeamTable() 
                {
                    TeamId=team.Id,
                    CreatedDateTime=team.CreatedDateTime.Value.UtcDateTime
                });
            }
            catch { }
        }

        private static async Task WatchTeamsAsync()
        {
            int count = 0;
            var deltaCollection = await graphClient.Groups
                .Delta()
                .Request()
                .Select("id,displayName,visibility,resourceProvisioningOptions,CreatedDateTime")
                .GetAsync();
            teamsDeltaCalls++;
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();

            while (true)
            {
                if (deltaCollection.CurrentPage.Count <= 0)
                {
                    Console.WriteLine("No changes on teams...");
                    await WatchTeamsSitesAsync();
                }
                else
                {
                    

                    var teamsFiltered = deltaCollection.CurrentPage.Where(w =>
                        w.ResourceProvisioningOptions != null
                    && w.ResourceProvisioningOptions.Contains("Team"));
                    Activity activity;
                    if (firstCall)
                        activity = Activity.Exist;
                    else
                        activity = Activity.Added;

                    var options = new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };

                    ConcurrentBag<string> resultCollection = new ConcurrentBag<string>();
                    ParallelLoopResult result = Parallel.ForEach(teamsFiltered, options, team =>
                    {
                        if (LogAddedTeam(team, activity))
                            resultCollection.Add(team.Id);
                    });
                    foreach (var teamId in resultCollection)
                        if (!teamSitesDeltaLinks.ContainsKey(teamId))
                        teamSitesDeltaLinks.Add(teamId, null);

                    
                    //if (result&&!teamSitesDeltaLinks.ContainsKey(team.Id))
                    //    teamSitesDeltaLinks.Add(team.Id, null);
                    //foreach (var team in teamsFiltered)
                    //{
                    //    await LogAddedTeamAsync(team, activity);
                    //    //count++;
                    //    //if (count > 10)
                    //    //    break;
                    //}

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

                watch.Stop();
                Console.WriteLine($"Checking Teams completed on {watch.ElapsedMilliseconds / 1000} seconds");

                await WatchTeamsSitesAsync();
                processTime = (int)DateTime.UtcNow.Subtract(lastProcessTime).TotalSeconds;
                lastProcessTime = DateTime.UtcNow;
                var wait = interval - processTime;
                if (wait < 0) wait = 0;
                await Task.Delay(wait * 1000);
                await DbOperations.UpdateResourcesAsync(resources);
                resources.Clear();
                firstCall = false;
                watch.Start();
                deltaCollection.InitializeNextPageRequest(graphClient, deltaLink);
                deltaCollection = await deltaCollection.NextPageRequest
                    .GetAsync();
                teamsDeltaCalls++;
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

        private static bool LogAddedTeam(Group team, Activity type)
        {
            try
            {
                //var teamSite= await graphClient.Groups[team.Id].Drive.Root
                //                        .Request()
                //                        .Select("CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                //                        .GetAsync();

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
                if (showOnConsole)
                    Console.WriteLine(record.Message);
                //Task.Delay(1 * 1000);
                return true;
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error WatchTeamSiteAsync team {team.DisplayName}: {exception.Message}");
                Console.ResetColor();
                //await graphClient.Groups[team.Id].Request().DeleteAsync();
                //Console.WriteLine($"team {team.DisplayName} deleted");
                return false;
            }
        }
        private static async Task WatchTeamsSitesAsync()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            Console.WriteLine("Start Checking Changes on Team Sites...");
            ICollection<string> keys = teamSitesDeltaLinks.Keys.ToList();
            if (multiThreading)
            {
                var options = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = noOfThreads
                };
                Parallel.ForEach(keys, options, async key =>
                 {
                     var pair = new KeyValuePair<string, DeltaLinks>(key, teamSitesDeltaLinks[key]);
                     await WatchTeamSiteAsync(pair);
                 });
            }
            else
            {
                foreach (var key in keys)
                {
                    var pair = new KeyValuePair<string, DeltaLinks>(key, teamSitesDeltaLinks[key]);
                    await WatchTeamSiteAsync(pair);
                }
            }
            noOfRuns++;
            watch.Stop();
            totalDuration += watch.ElapsedMilliseconds / 1000;
            Console.WriteLine($"Checking Changes on Team Sites completed on {watch.ElapsedMilliseconds/1000} seconds");

        }

        private static void WatchTeamSiteAsync(object pairObj)
        {
            var pair = (KeyValuePair<string, DeltaLinks>)pairObj;
            try
            {
                IDriveItemDeltaCollectionPage deltaCollection;
                var deltaLinks = new DeltaLinks
                {
                };

                if (pair.Value == null)
                {
                    deltaCollection = graphClient.Groups[pair.Key].Drive.Root
                        .Delta()
                        .Request()
                        .Select("CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                        .GetAsync().Result;
                    deltaLinks.DeltaCollection = deltaCollection;
                    deltaLinks.LastSyncDate = DateTime.UtcNow.Ticks / 100000000;
                    libraryDeltaCalls++;
                }
                else
                {
                    deltaLinks.LastSyncDate = pair.Value.LastSyncDate;
                    deltaCollection = pair.Value.DeltaCollection;
                    deltaCollection.InitializeNextPageRequest(graphClient, pair.Value.DeltaLink);
                    deltaCollection = deltaCollection.NextPageRequest
                        .GetAsync().Result;
                    libraryDeltaCalls++;
                }

                if (deltaCollection.CurrentPage.Count > 0)
                {
                    if (!firstCall)
                        foreach (var drive in deltaCollection.CurrentPage)
                            ProcessChangesAsync(drive, deltaLinks.LastSyncDate).Wait();
                    //else
                    //    foreach (var item in deltaCollection.CurrentPage)
                    //        Console.WriteLine($"{item.WebUrl}");
                }



                if (deltaCollection.AdditionalData.ContainsKey("@odata.nextLink") && deltaCollection.AdditionalData["@odata.nextLink"] != null)
                {
                    deltaLinks.DeltaLink = deltaCollection.AdditionalData["@odata.nextLink"].ToString();
                    deltaLinks.DeltaCollection = deltaCollection;
                    pair = new KeyValuePair<string, DeltaLinks>(pair.Key, deltaLinks);
                    WatchTeamSiteAsync(pair).Wait();
                }
                else if (deltaCollection.AdditionalData["@odata.deltaLink"] != null)
                {
                    deltaLinks.DeltaLink = deltaCollection.AdditionalData["@odata.deltaLink"].ToString();
                }
                deltaLinks.DeltaCollection = deltaCollection;
                deltaLinks.LastSyncDate = DateTime.UtcNow.Ticks / 100000000;
                teamSitesDeltaLinks[pair.Key] = deltaLinks;
            }
            catch (Exception exception)
            {
                if (exception.Message.Contains("Resource provisioning is in progress. Please try again.")
                    || exception.Message.Contains("Resource is not found."))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error WatchTeamSiteAsync site {pair.Key}: {exception.Message}");
                    Console.ResetColor();
                    //await Task.Delay(2 * 1000);
                    //await WatchTeamSiteAsync(pair);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error WatchTeamSiteAsync site {pair.Key}: {exception.Message}");
                    Console.ResetColor();
                }
            }
        }
        private static async Task WatchTeamSiteAsync(KeyValuePair<string, DeltaLinks> pair)
        {
            try
            {
                //var authProvider = new DeviceCodeAuthProvider();
                //var graphClient = new GraphServiceClient(authProvider);

                IDriveItemDeltaCollectionPage deltaCollection;
                var deltaLinks = new DeltaLinks
                {
                };

                if (pair.Value == null)
                {

                    //var item = await graphClient.Groups[pair.Key]
                    //    .Drive
                    //    .Request()
                    //    .Select("SharepointIds,WebUrl")
                    //    .GetAsync();
                    //List<QueryOption> queryOptions = new List<QueryOption>
                    //  {
                    //    new QueryOption("$filter", $@"WebUrl eq 'https://mmoustafa.sharepoint.com/sites/Public991/Shared Documents/'")
                    //  };
                    //deltaCollection = await graphClient.Sites[item.SharePointIds.SiteId].Drive.Root
                    //    .Delta()
                    //    .Request(queryOptions)
                    //    .Select("CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                    //    .GetAsync();

                    deltaCollection = await graphClient.Groups[pair.Key].Drive.Root
                        .Delta()
                        .Request()
                        .Select("CreatedDateTime,Deleted,File,Folder,LastModifiedDateTime,Root,SharepointIds,Size,WebUrl")
                        .GetAsync();
                    deltaLinks.DeltaCollection = deltaCollection;
                    deltaLinks.LastSyncDate = DateTime.UtcNow.Ticks/ 100000000;
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
                    deltaLinks.NoChanges = false;
                    if (!firstCall)
                    foreach (var drive in deltaCollection.CurrentPage)
                        await ProcessChangesAsync(drive, deltaLinks.LastSyncDate);
                    //else
                    //    foreach (var item in deltaCollection.CurrentPage)
                    //        Console.WriteLine($"{item.WebUrl}");
                }
                else
                {
                    deltaLinks.NoChanges = true;
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
                deltaLinks.LastSyncDate = DateTime.UtcNow.Ticks / 100000000;
                teamSitesDeltaLinks[pair.Key] = deltaLinks;
            }
            catch (Exception exception)
            {
                if (exception.Message.Contains("Resource provisioning is in progress. Please try again.")
                    || exception.Message.Contains("Resource is not found."))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error WatchTeamSiteAsync site {pair.Key}: {exception.Message}");
                    Console.ResetColor();
                    //await Task.Delay(2 * 1000);
                    //await WatchTeamSiteAsync(pair);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error WatchTeamSiteAsync site {pair.Key}: {exception.InnerException.Message}");
                    //Console.WriteLine($"{exception.InnerException}");
                    Console.ResetColor();
                    await Task.Delay(1000);
                    await WatchTeamSiteAsync(pair);
                }
            }
        }
        private static async Task ProcessChangesAsync(DriveItem drive, long lastSyncDate)
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error Process Changes {drive.WebUrl}: {exception.Message}");
                Console.ResetColor();
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
                //OrgActionDate=drive.CreatedDateTime.Value.UtcDateTime,
                //ObsActionDate=DateTime.UtcNow,
                //TimeDif = (int)DateTime.UtcNow.Subtract(drive.CreatedDateTime.Value.UtcDateTime).TotalSeconds,
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

        private static async Task LogResourceActivities(DriveItem drive, ResourceType resType, long lastSyncDate)
        {
            var spIds = drive.SharepointIds;

            var collection = await graphClient
                .Sites[spIds.SiteId]
                .Lists[spIds.ListId]
                .Items[spIds.ListItemId].Activities
                .Request()
                //.Top(5)
                //.Select("action,times")
                .GetAsync();

            activitiesCalls++;
            var activities = collection.Where(w => w.Times.RecordedDateTime.Value.Ticks>=lastSyncDate);

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

        private static bool FileChanged(DriveItem drive, long lastSyncDate)
        {
            return drive.File != null
                            && drive.CreatedDateTime != drive.LastModifiedDateTime
                            && (lastSyncDate <= drive.LastModifiedDateTime.Value.Ticks);
        }

        private static bool SkipFolder(DriveItem drive, long lastSyncDate)
        {
            return drive.Folder != null
                && drive.Deleted==null
                            && (lastSyncDate > drive.LastModifiedDateTime.Value.Ticks);
        }
        private static bool NewFile(DriveItem drive, long lastSyncDate)
        {
            return drive.File != null
                            && drive.CreatedDateTime == drive.LastModifiedDateTime
                            && (lastSyncDate<=drive.LastModifiedDateTime.Value.Ticks) ;
        }
        private static bool FolderChanged(DriveItem drive, long lastSyncDate)
        {
            return drive.Folder != null
                            && drive.Size == 0
                            && drive.CreatedDateTime != drive.LastModifiedDateTime
                            && (lastSyncDate <= drive.LastModifiedDateTime.Value.Ticks);
        }

        private static bool NewFolder(DriveItem drive, long lastSyncDate)
        {
            return drive.Folder != null
                            && drive.Size == 0
                            && drive.CreatedDateTime == drive.LastModifiedDateTime
                            && (lastSyncDate <= drive.LastModifiedDateTime.Value.Ticks);
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
