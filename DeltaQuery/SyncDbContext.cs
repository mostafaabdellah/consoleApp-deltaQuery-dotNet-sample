// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace DeltaQuery
{
    public class SyncDbContext : DbContext
    {
        public DbSet<Resource> Resources { get; set; }
        public DbSet<Source> Sources { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=SyncDb;Integrated Security=SSPI;AttachDBFilename=C:\Users\mmoustafa\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\SyncDb.mdf");
        }
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

    public class DbOperations
    {
        public static async Task UpdateSourcesAsync(List<Source> sources)
        {
            using (var context = new SyncDbContext())
            {
                context.Sources.AddRange(sources);
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
    }
}
