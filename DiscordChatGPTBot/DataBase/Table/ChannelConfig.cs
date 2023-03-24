namespace DiscordChatGPTBot.DataBase.Table
{
    public class ChannelConfig : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public bool IsEnable { get; set; } = true;
        public string SystemPrompt { get; set; } = "你是一個有幫助的助手。";
        public string ChatGPTModel { get; set; } = "gpt-3.5-turbo";
        public uint ResetDeltaTime { get; set; } = 3600;
        public uint MaxTurns { get; set; } = 10;
    }
}
