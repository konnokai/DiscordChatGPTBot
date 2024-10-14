namespace DiscordChatGPTBot.DataBase.Table
{
    public class ChannelConfig : DbEntity
    {
        public enum ChatGPTModel
        {
            GPT4o = 0,
            GPT4o_Mini = 1
        }

        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public bool IsEnable { get; set; } = true;
        public bool IsInheritChatWhenReset { get; set; } = false;
        public string SystemPrompt { get; set; } = "你是一個有幫助的助手。使用繁體中文回答問題。";
        public string CompletedEmoji { get; set; } = ":ok:";
        public ChatGPTModel UsedChatGPTModel { get; set; } = ChatGPTModel.GPT4o_Mini;
        public uint ResetDeltaTime { get; set; } = 3600;
        public uint MaxTurns { get; set; } = 10;
    }
}
