using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordChatGPTBot.Migrations
{
    public partial class AddChatHistroy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatHistroy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    UserPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    ChatUseTokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultUseTokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotlaUseTokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatHistroy", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatHistroy");
        }
    }
}
