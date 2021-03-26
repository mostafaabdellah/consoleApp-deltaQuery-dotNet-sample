// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using Microsoft.EntityFrameworkCore;

namespace DeltaQuery
{
    public class SyncDbContext : DbContext
    {
        public DbSet<Resource> Resources { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=SyncDb;Integrated Security=SSPI;AttachDBFilename=C:\Users\mmoustafa\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\SyncDb.mdf");
        }
    }
   
}
