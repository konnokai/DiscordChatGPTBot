﻿// <auto-generated />
using System;
using DiscordChatGPTBot.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordChatGPTBot.Migrations
{
    [DbContext(typeof(MainDbContext))]
    [Migration("20230331032433_AddIsInheritChatWhenReset")]
    partial class AddIsInheritChatWhenReset
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
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

                    b.Property<string>("ChatGPTModel")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("CompletedEmoji")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("DateAdded")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsEnable")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsInheritChatWhenReset")
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

            modelBuilder.Entity("DiscordChatGPTBot.DataBase.Table.GuildConfig", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("DateAdded")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("OpenAIKey")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("GuildConfig");
                });
#pragma warning restore 612, 618
        }
    }
}
