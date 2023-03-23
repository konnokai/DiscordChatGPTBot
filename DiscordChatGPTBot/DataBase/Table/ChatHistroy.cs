namespace DiscordChatGPTBot.DataBase.Table
{
    public class ChatHistroy : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong UserId { get; set; }
        public string? SystemPrompt { get; set; }
        public string? UserPrompt { get; set; }
        public int ChatUseTokenCount { get; set; } = 0;
        public int ResultUseTokenCount { get; set; } = 0;
        public int TotlaUseTokenCount { get; set; } = 0;
    }
}
