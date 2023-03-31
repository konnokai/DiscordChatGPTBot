namespace DiscordChatGPTBot.DataBase.Table
{
    public class ChannelConfig : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public bool IsEnable { get; set; } = true;
        public bool IsInheritChatWhenReset { get; set; } = false;
        public string SystemPrompt { get; set; } = "你是一個有幫助的助手。使用繁體中文回答問題。";
        public string CompletedEmoji { get; set; } = ":ok:";
        public string ChatGPTModel { get; set; } = "gpt-3.5-turbo";
        public uint ResetDeltaTime { get; set; } = 3600;
        public uint MaxTurns { get; set; } = 10;
    }
}
