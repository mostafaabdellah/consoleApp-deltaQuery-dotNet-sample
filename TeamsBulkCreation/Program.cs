using DeltaQuery;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
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
        private static int teamsRequestedCount = 2500;
        private static int folderRequestedCount = 20;
        private static int filesCount = 20;
        //private static int folderRequestedLevelCount = 1;
        //private static int folderCounter = 1;
        //private static int folderLevelCounter = 1;
        private static int trials = 2;
        private static bool showOnConsole = true;
        private static IDictionary<string, SharepointIds> teamSites = new Dictionary<string, SharepointIds>();
        private static Source.Activity currentActivity = Source.Activity.Unknown;
        static async Task Main(string[] args)
        {
            //var appAuthProvider = new AppAuthProvider();
            var accountAuthProvider = new AccountAuthProvider();
            graphClient = new GraphServiceClient(accountAuthProvider);
            await ClearTeamsAndSourcesAsync();
            await CreateTeamsAsync();
            await LogTeamsAsync();

            graphClient = new GraphServiceClient(accountAuthProvider);
            currentActivity = Source.Activity.Added;
            await CreateTeamFoldersAsync();
            currentActivity = Source.Activity.VersionAdded;
            await CreateTeamFoldersAsync();
        }

        private static async Task CreateTeamFoldersAsync()
        {
            foreach (var key in teamSites.Keys)
            {
                await CreateFolderLevelAsync(teamSites[key], "", 1, 1);
                for (int i = 1; i <= folderRequestedCount; i++)
                {
                    var path = await CreateFolderLevelAsync(teamSites[key], "/General",i,1);
                    for (int j = 1; j <= folderRequestedCount; j++)
                    {
                        await CreateFolderLevelAsync(teamSites[key], path,i,j);
                    }
                }
            }
        }

        private static async Task<string> CreateFolderLevelAsync(SharepointIds sp, string path, int l1, int l2)
        {
            DriveItem createdFolder;
            var driveItem = new DriveItem
            {
                Name = (l1==1&& l2==1)? $"Folder{l1:D2}":$"Folder{l1:D2}L{l2:D2}",
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

            for (int i = 1; i <= filesCount; i++)
                await UploadSmallFile(sp, path,i);

            for (int i = 1; i <= filesCount; i++)
                await CheckoutFile(sp, path, i);


            //await UploadlargeFile(sp, path);
            return path;
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
        private static async Task CheckoutFile(SharepointIds sp, string path, int i)
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
            var teams = await graphClient.Groups
                .Request()
                .Filter("resourceProvisioningOptions/Any(x:x eq 'Team')")
                .Select("id")
                .GetAsync();

            foreach (var team in teams)
                await graphClient.Groups[team.Id].Request().DeleteAsync();

            await DbOperations.ClearSourcesAsync();
        }

        private static async Task LogTeamsAsync()
        {
            try
            {
                var teams = await graphClient.Groups
                    .Request()
                    .Filter("resourceProvisioningOptions/Any(x:x eq 'Team')")
                    .Select("id,displayName,visibility,resourceProvisioningOptions,CreatedDateTime")
                    .GetAsync();

                foreach (var team in teams)
                    await LogTeamAsync(team);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private static async Task LogTeamAsync(Group team)
        {
            try
            {
                var item = await graphClient.Groups[team.Id]
                    .Drive
                    .Request()
                    .Select("SharepointIds,WebUrl")
                    .GetAsync();


                var source = new Source()
                {
                    ActType = Source.Activity.Added,
                    ResType = Source.ResourceType.Team,
                    OrgActionDate = team.CreatedDateTime.Value.UtcDateTime,
                    Message = $"New Team Created {team.DisplayName} on {team.CreatedDateTime.Value.UtcDateTime}"
                };
                await DbOperations.UpdateSourcesAsync(source);

                teamSites.Add(team.Id, item.SharePointIds);
                if (showOnConsole)
                    Console.WriteLine(source.Message);
                trials = 100;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //Thread.Sleep(2000);
                //trials--;
                //await LogTeamAsync(team);
            }

        }

        private static async Task CreateTeamsAsync()
        {
            for (int i = 0; i < teamsRequestedCount; i++)
            {
                await NewTeamAsync($"Public {i:D3}", TeamVisibilityType.Public);
            }
            for (int i = 0; i < teamsRequestedCount; i++)
            {
                await NewTeamAsync($"Private {i:D3}", TeamVisibilityType.Private);
            }
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
