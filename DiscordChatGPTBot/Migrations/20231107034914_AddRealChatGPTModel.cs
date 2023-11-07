using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordChatGPTBot.Migrations
{
    /// <inheritdoc />
    public partial class AddRealChatGPTModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatGPTModel",
                table: "ChannelConfig");

            migrationBuilder.AddColumn<int>(
                name: "UsedChatGPTModel",
                table: "ChannelConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsedChatGPTModel",
                table: "ChannelConfig");

            migrationBuilder.AddColumn<string>(
                name: "ChatGPTModel",
                table: "ChannelConfig",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
