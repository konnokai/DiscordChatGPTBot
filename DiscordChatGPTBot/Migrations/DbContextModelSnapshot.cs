﻿// <auto-generated />
using System;
using DiscordChatGPTBot.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordChatGPTBot.Migrations
{
    [DbContext(typeof(MainDbContext))]
    partial class DbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.15");

            modelBuilder.Entity("DiscordChatGPTBot.DataBase.Table.ChannelConfig", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("DateAdded")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsEnable")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("MaxTurns")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("ResetDeltaTime")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SystemPrompt")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("ChannelConfig");
                });

            modelBuilder.Entity("DiscordChatGPTBot.DataBase.Table.ChatHistroy", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ChatUseTokenCount")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("DateAdded")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ResultUseTokenCount")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SystemPrompt")
                        .HasColumnType("TEXT");

                    b.Property<int>("TotlaUseTokenCount")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("UserId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UserPrompt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("ChatHistroy");
                });
#pragma warning restore 612, 618
        }
    }
}
