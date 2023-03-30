﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordChatGPTBot.Migrations
{
    public partial class AddCompletedEmoji : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletedEmoji",
                table: "ChannelConfig",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedEmoji",
                table: "ChannelConfig");
        }
    }
}
