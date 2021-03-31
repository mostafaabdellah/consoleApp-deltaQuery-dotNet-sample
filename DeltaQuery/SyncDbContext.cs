// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DeltaQuery
{
    public class SyncDbContext : DbContext
    {
        public DbSet<Resource> Resources { get; set; }
        public DbSet<Source> Sources { get; set; }
        public DbSet<Performance> Performances { get; set; }
        public DbSet<TeamTable> TeamsTable { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=SyncDb;Integrated Security=SSPI;AttachDBFilename=C:\Users\mmoustafa\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\SyncDb.mdf");
        }
    }
    public class TeamTable
    {
        [Key]
        public Guid Id { get; set; }
        public string TeamId { get; set; }
        public DateTime CreatedDateTime { get; set; }
    }
        public class Source
    {
        [Key]
        public Guid Id { get; set; }
        public string SiteUrl { get; set; }
        public string WebUrl { get; set; }
        public string SiteId { get; set; }
        public string ListId { get; set; }
        public string ListItemUniqueId { get; set; }
        public ResourceType ResType { get; set; }
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
            Exist = 7,
            Unknown = 8
        }
        public enum ResourceType
        {
            Team = 1,
            Folder = 2,
            File = 3
        }
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
        public ResourceType ResType { get; set; }
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
            Exist = 7,
            Unknown = 8
        }
        public enum ResourceType
        {
            Team = 1,
            Folder = 2,
            File = 3
        }
    }
    public class Performance
    {
        [Key]
        public Guid Id { get; set; }
        public int TeamsCount { get; set; }
        public int DeltaCalls { get; set; }
        public int ActivitiesCalls { get; set; }
        public DateTime StartOn { get; set; }
        public DateTime CompletedOn { get; set; }
        public int Duration { get; set; }
        public int AverageSyncDuration { get; set; }
        public long TotalDuration { get; set; }
        public int NoOfRuns { get; set; }
        public long AvgDuration { get; set; }
    }
    public class DbOperations
    {
        public static async Task AddTeamsToTable(IList<TeamTable> teams)
        {
            using (var context = new SyncDbContext())
            {
                context.TeamsTable.AddRange(teams);
                await context.SaveChangesAsync();
            }
        }
        public static IList<TeamTable> GetTeams(int limit)
        {
            using (var context = new SyncDbContext())
            {
                return context.TeamsTable.OrderBy(o => o.CreatedDateTime).Take(limit).ToList();
            }
        }
        public static async Task UpdateResourcesAsync(IList<Resource> resources)
        {
            using (var context = new SyncDbContext())
            {
                context.Resources.AddRange(resources);
                await context.SaveChangesAsync();
            }
        }

        public static async Task ClearSourcesAsync()
        {
            using (var context = new SyncDbContext())
            {
                await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE Sources");
            }
        }

        

        internal static async Task ClearResourcesAsync()
        {
            using (var context = new SyncDbContext())
            {
                await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE Resources");
            }
        }

        public static async Task UpdateSourcesAsync(Source source)
        {
            using (var context = new SyncDbContext())
            {
                context.Sources.Add(source);
                await context.SaveChangesAsync();
            }
        }
        public static async Task UpdatePerformanceAsync(Performance per)
        {
            using (var context = new SyncDbContext())
            {
                context.Performances.Update(per);
                await context.SaveChangesAsync();
            }
        }

        internal static int GetAverageSync()
        {
            try
            {
                using (var context = new SyncDbContext())
                {
                    return (int)context.Resources.ToList().Where(w => w.TimeDif < 50).Average(a => a.TimeDif);
                }
            }
            catch { return 0; }
        }
    }
}
