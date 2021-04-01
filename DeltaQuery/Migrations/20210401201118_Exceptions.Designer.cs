﻿// <auto-generated />
using System;
using DeltaQuery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DeltaQuery.Migrations
{
    [DbContext(typeof(SyncDbContext))]
    [Migration("20210401201118_Exceptions")]
    partial class Exceptions
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.4")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("DeltaQuery.Exceptions", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("CallStack")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedDateTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("Exception")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("InnerException")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Exceptions");
                });

            modelBuilder.Entity("DeltaQuery.Performance", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("ActivitiesCalls")
                        .HasColumnType("int");

                    b.Property<int>("AverageSyncDuration")
                        .HasColumnType("int");

                    b.Property<long>("AvgDuration")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CompletedOn")
                        .HasColumnType("datetime2");

                    b.Property<int>("DeltaCalls")
                        .HasColumnType("int");

                    b.Property<int>("Duration")
                        .HasColumnType("int");

                    b.Property<int>("NoOfRuns")
                        .HasColumnType("int");

                    b.Property<DateTime>("StartOn")
                        .HasColumnType("datetime2");

                    b.Property<int>("TeamsCount")
                        .HasColumnType("int");

                    b.Property<long>("TotalDuration")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("Performances");
                });

            modelBuilder.Entity("DeltaQuery.Resource", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("ActType")
                        .HasColumnType("int");

                    b.Property<string>("ListId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ListItemUniqueId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Message")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("ObsActionDate")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("OrgActionDate")
                        .HasColumnType("datetime2");

                    b.Property<int>("ResType")
                        .HasColumnType("int");

                    b.Property<string>("SiteId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SiteUrl")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TimeDif")
                        .HasColumnType("int");

                    b.Property<string>("WebUrl")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Resources");
                });

            modelBuilder.Entity("DeltaQuery.Source", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("ActType")
                        .HasColumnType("int");

                    b.Property<string>("ListId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ListItemUniqueId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Message")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("ObsActionDate")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("OrgActionDate")
                        .HasColumnType("datetime2");

                    b.Property<int>("ResType")
                        .HasColumnType("int");

                    b.Property<string>("SiteId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SiteUrl")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TimeDif")
                        .HasColumnType("int");

                    b.Property<string>("WebUrl")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Sources");
                });

            modelBuilder.Entity("DeltaQuery.TeamTable", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedDateTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("TeamId")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("TeamsTable");
                });
#pragma warning restore 612, 618
        }
    }
}
