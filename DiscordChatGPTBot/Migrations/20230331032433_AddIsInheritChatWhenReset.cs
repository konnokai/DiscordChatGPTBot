using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordChatGPTBot.Migrations
{
    public partial class AddIsInheritChatWhenReset : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInheritChatWhenReset",
                table: "ChannelConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInheritChatWhenReset",
                table: "ChannelConfig");
        }
    }
}
