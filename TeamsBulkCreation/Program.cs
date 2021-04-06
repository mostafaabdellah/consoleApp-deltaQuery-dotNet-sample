using DeltaQuery;
using Microsoft.Graph;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeamsBulkCreation.Authentication;
using static DeltaQuery.Resource;

namespace TeamsBulkCreation
{
    class Program
    {
        const string smallFilePath = @"SampleFiles\SmallFile.txt";
        const string largeFilePath = @"SampleFiles\LargeFile.txt";

        private static GraphServiceClient graphClient;
        private static int teamsRequestedCount = 6000;
        private static int startTeamId = 5195;
        private static int folderRequestedCount = 2;
        private static int filesCount = 3;
        //private static int folderRequestedLevelCount = 1;
        //private static int folderCounter = 1;
        //private static int folderLevelCounter = 1;
        private static int trials = 2;
        private static bool showOnConsole = true;
        private static ConcurrentDictionary<string, SharepointIds> teamSites = new ConcurrentDictionary<string, SharepointIds>();
        private static Source.Activity currentActivity = Source.Activity.Unknown;
        private static List<Group> allTeams = new List<Group>();
        private static int fileId = 1006;
        private static int noTeams = 5000;
        private static int pageSize = 100;
        private static int pageNumber = 0;
        private static int noOfThreads = 4;
        private static bool muliThreading = false;
        static async Task Main(string[] args)
        {
            var rand = new Random();
            fileId = rand.Next(1, int.MaxValue);

            var accountAuthProvider = new AccountAuthProvider();
            var appAuthProvider = new AppAuthProvider();
            graphClient = new GraphServiceClient(accountAuthProvider);
            //await ClearTeamsAndSourcesAsync();
            //await CreateTeamsAsync();
            //await DbOperations.ClearSourcesAsync();
            await LogTeamsAsync();
            //await DeleteTeamGeneralFolderAsync("/General");
            //graphClient = new GraphServiceClient(accountAuthProvider);
            await CreateTeamMutiThreadFoldersAsync();
            //await CreateTeamFoldersAsync();

        }

        private static async Task CreateTeamMutiThreadFoldersAsync()
        {
            while (true)
            {
                var rand = new Random();
                fileId = rand.Next(1, int.MaxValue);

                var pages = noTeams / pageSize;
                var options = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = noOfThreads
                };

                for (int i = 0; i < pages; i++)
                {
                    IEnumerable<string> keys = teamSites.Keys.Skip(i * pageSize).Take(pageSize);
                    if (muliThreading)
                        Parallel.ForEach(keys, options, async key =>
                          {
                              await UploadSmallFileMT(teamSites[key], "/", fileId);
                          });
                    else
                        foreach (var key in keys)
                            await UploadSmallFileMT(teamSites[key], "/", fileId);

                }
                await Task.Delay(300 * 1000);
            }
        }
        private static async Task UploadSmallFileMT(SharepointIds sp, string path, int i)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    var writer = new StreamWriter(stream);
                    writer.Write("Test document");
                    writer.Flush();
                    stream.Position = 0;

                    var createdFile = await graphClient.Sites[sp.SiteId]
                                .Drive.Root
                                .ItemWithPath($"{path}/SmallFile{i:D2}.txt")
                                .Content.Request()
                                .PutAsync<DriveItem>(stream);
                    var source = new Source()
                    {
                        ActType = currentActivity,
                        ResType = Source.ResourceType.File,
                        OrgActionDate = createdFile.CreatedDateTime.Value.UtcDateTime,
                        Message = $"New File Created {createdFile.WebUrl} on {createdFile.CreatedDateTime.Value.UtcDateTime}"
                    };
                    //await DbOperations.UpdateSourcesAsync(source);
                    if (showOnConsole)
                        Console.WriteLine(source.Message);
                }
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private static async Task CreateTeamFoldersAsync()
        {
            foreach (var key in teamSites.Keys)
            {
                await UploadSmallFile(teamSites[key], "/", fileId);
                //await CreateSingleFileAsync(teamSites[key], "", 1, 1);
                //for (int i = 1; i <= folderRequestedCount; i++)
                //{
                //    var path = await CreateFolderLevelAsync(teamSites[key], "/General",i,1);
                //    for (int j = 1; j <= folderRequestedCount; j++)
                //    {
                //        await CreateFolderLevelAsync(teamSites[key], path,i,j);
                //    }
                //}
            }
        }

        private static async Task<string> CreateSingleFileAsync(SharepointIds sp, string path, int l1, int l2)
        {
            try
            {
                DriveItem createdFolder;
                var driveItem = new DriveItem
                {
                    Name = (l1 == 1 && l2 == 1) ? $"Folder{l1:D2}" : $"Folder{l1:D2}L{l2:D2}",
                    Folder = new Folder
                    {
                    },
                    AdditionalData = new Dictionary<string, object>()
                {
                    {"@microsoft.graph.conflictBehavior", "replace"}
                }
                };
                if (string.IsNullOrEmpty(path))
                {
                    driveItem.Name = "General";
                    createdFolder = await graphClient.Sites[sp.SiteId]
                    .Drive.Root.Children.Request()
                    .AddAsync(driveItem);
                }
                else
                {
                    createdFolder = await graphClient.Sites[sp.SiteId]
                        .Drive.Root.ItemWithPath(path)
                        .Children
                        .Request()
                        .AddAsync(driveItem);
                }
                path = $"{createdFolder.ParentReference.Path.Split(':')[1]}/{createdFolder.Name}";
                await UploadSmallFile(sp, path, 1);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return path;

        }
        private static async Task<string> CreateFolderLevelAsync(SharepointIds sp, string path, int l1, int l2)
        {
            try
            {
                DriveItem createdFolder;
                var driveItem = new DriveItem
                {
                    Name = (l1 == 1 && l2 == 1) ? $"Folder{l1:D2}" : $"Folder{l1:D2}L{l2:D2}",
                    Folder = new Folder
                    {
                    },
                    AdditionalData = new Dictionary<string, object>()
                {
                    {"@microsoft.graph.conflictBehavior", "replace"}
                }
                };
                if (string.IsNullOrEmpty(path))
                {
                    driveItem.Name = "General";
                    createdFolder = await graphClient.Sites[sp.SiteId]
                    .Drive.Root.Children.Request()
                    .AddAsync(driveItem);
                }
                else
                {
                    createdFolder = await graphClient.Sites[sp.SiteId]
                        .Drive.Root.ItemWithPath(path)
                        .Children
                        .Request()
                        .AddAsync(driveItem);
                }
                path = $"{createdFolder.ParentReference.Path.Split(':')[1]}/{createdFolder.Name}";

                var source = new Source()
                {
                    ActType = currentActivity,
                    ResType = Source.ResourceType.Folder,
                    OrgActionDate = createdFolder.CreatedDateTime.Value.UtcDateTime,
                    Message = $"New Folder Created {createdFolder.WebUrl} on {createdFolder.CreatedDateTime.Value.UtcDateTime}"
                };
                await DbOperations.UpdateSourcesAsync(source);
                if (showOnConsole)
                    Console.WriteLine(source.Message);

                await Task.Delay(100);

                for (int i = 1; i <= filesCount; i++)
                    await UploadSmallFile(sp, path, i);

                await Task.Delay(100);

                for (int i = 1; i <= filesCount; i++)
                    await CheckOutInFile(sp, path, i);

                await Task.Delay(100);

                for (int i = 1; i <= filesCount; i++)
                    await RenameFile(sp, path, i);

                await Task.Delay(5000);

                for (int i = 1; i <= filesCount; i++)
                    await DeleteFile(sp, path, i);
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return path;

        }

        private static async Task RenameFile(SharepointIds sp, string path, int i)
        {
            var driveItem = new DriveItem
            {
                Name = $"RenameSmallFile{i:D2}.txt"
            };

            await graphClient.Sites[sp.SiteId]
                            .Drive.Root
                            .ItemWithPath($"{path}/SmallFile{i:D2}.txt")
                            .Request()
                            .UpdateAsync(driveItem);

            var source = new Source()
            {
                ActType = Source.Activity.Renamed,
                ResType = Source.ResourceType.File,
                OrgActionDate = DateTime.UtcNow,
                Message = $"Rename file {$"{path}/SmallFile{i:D2}.txt"} on {DateTime.UtcNow}"
            };
            await DbOperations.UpdateSourcesAsync(source);
            if (showOnConsole)
                Console.WriteLine(source.Message);
        }
        private static async Task DeleteFile(SharepointIds sp, string path, int i)
        {
            await graphClient.Sites[sp.SiteId]
                            .Drive.Root
                            .ItemWithPath($"{path}/RenameSmallFile{i:D2}.txt")
                            .Request()
                            .DeleteAsync();

            var source = new Source()
            {
                ActType = Source.Activity.Deleted,
                ResType = Source.ResourceType.File,
                OrgActionDate = DateTime.UtcNow,
                Message = $"Rename file {$"{path}/RenameSmallFile{i:D2}.txt"} on {DateTime.UtcNow}"
            };
            await DbOperations.UpdateSourcesAsync(source);
            if (showOnConsole)
                Console.WriteLine(source.Message);
        }

        private static async Task UploadSmallFile(SharepointIds sp, string path, int i)
        {
            using (FileStream fileStream = new FileStream(smallFilePath, FileMode.Open))
            {
                var createdFile= await graphClient.Sites[sp.SiteId]
                            .Drive.Root
                            .ItemWithPath($"{path}/SmallFile{i:D2}.txt")
                            .Content.Request()
                            .PutAsync<DriveItem>(fileStream);
                var source = new Source()
                {
                    ActType = currentActivity,
                    ResType = Source.ResourceType.File,
                    OrgActionDate = createdFile.CreatedDateTime.Value.UtcDateTime,
                    Message = $"New File Created {createdFile.WebUrl} on {createdFile.CreatedDateTime.Value.UtcDateTime}"
                };
                await DbOperations.UpdateSourcesAsync(source);
                if (showOnConsole)
                    Console.WriteLine(source.Message);
            }
        }
        private static async Task CheckOutInFile(SharepointIds sp, string path, int i)
        {
            using (FileStream fileStream = new FileStream(smallFilePath, FileMode.Open))
            {
                await graphClient.Sites[sp.SiteId]
                            .Drive.Root
                            .ItemWithPath($"{path}/SmallFile{i:D2}.txt")
                            .Checkout()
                            .Request()
                            .PostAsync();

                await graphClient.Sites[sp.SiteId]
                            .Drive.Root
                            .ItemWithPath($"{path}/SmallFile{i:D2}.txt")
                            .Checkin()
                            .Request()
                            .PostAsync();

                var source = new Source()
                {
                    ActType = Source.Activity.VersionAdded,
                    ResType = Source.ResourceType.File,
                    OrgActionDate = DateTime.UtcNow,
                    Message = $"New Version Created {$"{path}/SmallFile{i:D2}.txt"} on {DateTime.UtcNow}"
                };
                await DbOperations.UpdateSourcesAsync(source);
                if (showOnConsole)
                    Console.WriteLine(source.Message);
            }
        }
        private static async Task<DriveItem> UploadlargeFile(SharepointIds sp, string path)
        {
            DriveItem uploadedFile = null;
            using (FileStream fileStream = new FileStream(largeFilePath, FileMode.Open))
            {
                UploadSession uploadSession = await graphClient.Sites["root"]
                    .Drive.Root.ItemWithPath($"{path}/LargeFile.txt")
                    .CreateUploadSession().Request()
                    .PostAsync();

                if (uploadSession != null)
                {
                    // Chunk size must be divisible by 320KiB, our chunk size will be slightly more than 1MB
                    int maxSizeChunk = (320 * 1024) * 4;
                    ChunkedUploadProvider uploadProvider = new ChunkedUploadProvider(uploadSession, graphClient, fileStream, maxSizeChunk);
                    var chunkRequests = uploadProvider.GetUploadChunkRequests();
                    var exceptions = new List<Exception>();
                    var readBuffer = new byte[maxSizeChunk];
                    foreach (var request in chunkRequests)
                    {
                        var result = await uploadProvider.GetChunkRequestResponseAsync(request, readBuffer, exceptions);
                        if (result.UploadSucceeded)
                        {
                            uploadedFile = result.ItemResponse;
                        }
                    }
                }
            }
            return uploadedFile;
        }

        private static async Task ClearTeamsAndSourcesAsync()
        {
            //var teams = await graphClient.Groups
            //    .Request()
            //    .Filter("resourceProvisioningOptions/Any(x:x eq 'Team')")
            //    .Select("id")
            //    .GetAsync();

            //foreach (var team in teams)
            //    await graphClient.Groups[team.Id].Request().DeleteAsync();

            await DbOperations.ClearSourcesAsync();
        }
        private static async Task GetTeams()
        {


            var deltaCollection = await graphClient.Groups
                               .Request()
                               .Filter("resourceProvisioningOptions/Any(x:x eq 'Team')")
                               .Select("id,displayName,CreatedDateTime")
                               .GetAsync();
            foreach (Group team in deltaCollection)
            {
                allTeams.Add(team);
            }
            while (deltaCollection.AdditionalData.ContainsKey("@odata.nextLink")
                    && deltaCollection.AdditionalData["@odata.nextLink"] != null)
            {
                var nextLink = deltaCollection.AdditionalData["@odata.nextLink"].ToString();
                deltaCollection.InitializeNextPageRequest(graphClient, nextLink);
                deltaCollection = await deltaCollection.NextPageRequest
                    .GetAsync();

                foreach (Group team in deltaCollection)
                {
                    allTeams.Add(team);
                }
            }

        }
        private static async Task LogTeamsAsync()
        {
            try
            {
                //await GetTeams();
                //var teams = allTeams.OrderBy(o => o.CreatedDateTime).Take(limit);

                var teams = DbOperations.GetTeams(noTeams);//.Skip(pageNumber*pageSize).Take(pageSize);
                _ = LogTeamAsync("").Result;
                var options = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = noOfThreads
                };
                if (muliThreading)
                    Parallel.ForEach(teams, options, team =>
                    {
                        var spid = LogTeamAsync(team.TeamId).Result;
                        teamSites.TryAdd(team.TeamId, spid);

                    });
                else
                    foreach (var team in teams)
                    {
                        var spid = LogTeamAsync(team.TeamId).Result;
                        teamSites.TryAdd(team.TeamId, spid);
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private static async Task DeleteTeamGeneralFolderAsync(string path)
        {
            try
            {
                foreach (var team in teamSites)
                    await DeleteFolder(path, team.Key);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static async Task DeleteFolder(string path, string teamId)
        {
            try
            {
                await graphClient.Sites[teamId]
                                           .Drive.Root
                                           .ItemWithPath($"{path}")
                                           .Request()
                                           .DeleteAsync();
                var source = new Source()
                {
                    ActType = currentActivity,
                    ResType = Source.ResourceType.File,
                    OrgActionDate = DateTime.UtcNow,
                    Message = $"{path} Folder Deleted"
                };

                await DbOperations.UpdateSourcesAsync(source);
                if (showOnConsole)
                    Console.WriteLine(source.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static async Task<SharepointIds> LogTeamAsync(string teamId)
        {
            try
            {
                var item = await graphClient.Groups[teamId]
                    .Drive
                    .Request()
                    .Select("Id,SharepointIds,WebUrl")
                    .GetAsync();


                //var source = new Source()
                //{
                //    ActType = Source.Activity.Added,
                //    ResType = Source.ResourceType.Team,
                //    OrgActionDate = team.CreatedDateTime.Value.UtcDateTime,
                //    Message = $"New Team Created {team.DisplayName} on {team.CreatedDateTime.Value.UtcDateTime}"
                //};
                //await DbOperations.UpdateSourcesAsync(source);
                return item.SharePointIds;
                //if (showOnConsole)
                //    Console.WriteLine(source.Message);
                //trials = 100;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
                //Thread.Sleep(2000);
                //trials--;
                //await LogTeamAsync(team);
            }

        }

        private static async Task CreateTeamsAsync()
        {
            for (int i = startTeamId; i <= teamsRequestedCount; i++)
            {
                await NewTeamAsync($"Public {i:D3}", TeamVisibilityType.Public);
            }
            //for (int i = startTeamId; i < teamsRequestedCount; i++)
            //{
            //    await NewTeamAsync($"Private {i:D3}", TeamVisibilityType.Private);
            //}
        }

        private static async Task NewTeamAsync(string name, TeamVisibilityType visibilityType)
        {
            var team = new Team
            {
                DisplayName = name,
                Description = $"{name} Description",
                Visibility = visibilityType,
                AdditionalData = new Dictionary<string, object>()
                {
                    { "template@odata.bind", "https://graph.microsoft.com/v1.0/teamsTemplates('standard')"}
                },
                Members = new TeamMembersCollectionPage()
                {
                    new AadUserConversationMember
                    {
                        Roles = new List<string>(){"owner"},
                        AdditionalData = new Dictionary<string, object>()
                        {
                            {"user@odata.bind", "https://graph.microsoft.com/v1.0/users('636b5365-803f-4703-8cd8-a39d473fe31a')"}
                        }
                    }
                }
            };

            await graphClient.Teams
                .Request()
                .AddAsync(team);
            Console.WriteLine($"Team \"{team.DisplayName}\" Created");
            await Task.Delay(1000);
        }
    }
}
