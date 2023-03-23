using Discord.Interactions;
using DiscordChatGPTBot.DataBase.Table;

namespace DiscordChatGPTBot.Interaction.OpenAI
{
    public class OpenAI : TopLevelModule<Service.OpenAIService>
    {
        [SlashCommand("init", "初始化")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Initialization([Summary("system-prompt", "人設，可使用 \"/set-system-prompt\" 變更")] string prompt = "你是一個有幫助的助手。")
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                ChannelConfig? channelConfig = db.ChannelConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id && x.ChannelId == Context.Channel.Id);
                if (channelConfig != null)
                {
                    await Context.Interaction.SendErrorAsync("本頻道已啟用");
                    return;
                }

                db.ChannelConfig.Add(new ChannelConfig() { GuildId = Context.Guild.Id, ChannelId = Context.Channel.Id, SystemPrompt = prompt });
                db.SaveChanges();

                _service.RefreshChannelConfig();

                await Context.Interaction.SendConfirmAsync($"已在此頻道啟用AI對話功能\n" +
                    $"如需更改人設請使用 `/set-system-prompt`\n" +
                    $"如需關閉請使用 `/toggle`\n" +
                    $"AI人設:\n" +
                    $"```\n" +
                    $"{prompt}\n" +
                    $"```");
            }
        }

        [SlashCommand("set-system-prompt", "設定AI的人設")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSystemPrompt(string prompt)
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                ChannelConfig? channelConfig = db.ChannelConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id && x.ChannelId == Context.Channel.Id);
                if (channelConfig == null)
                {
                    await Context.Interaction.SendErrorAsync("本頻道尚未啟用AI聊天功能，請使用 `/init` 後再試");
                    return;
                }

                channelConfig.SystemPrompt = prompt;
                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync("已更新AI人設:\n" +
                    $"```\n" +
                    $"{prompt}\n" +
                    $"```");

                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("toggle", "切換開關")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Toggle()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                ChannelConfig? channelConfig = db.ChannelConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id && x.ChannelId == Context.Channel.Id);
                if (channelConfig == null)
                {
                    await Context.Interaction.SendErrorAsync("本頻道尚未啟用AI聊天功能，請使用 `/init` 後再試");
                    return;
                }

                channelConfig.IsEnable = !channelConfig.IsEnable;
                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync("已切換AI聊天功能為: " + (channelConfig.IsEnable ? "開啟" : "關閉"));
                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("say", "跟AI對話")]
        [RequireContext(ContextType.Guild)]
        public async Task Say(string message)
        {
            ChannelConfig? channelConfig;
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                channelConfig = db.ChannelConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id && x.ChannelId == Context.Channel.Id);
                if (channelConfig == null)
                {
                    await Context.Interaction.SendErrorAsync("本頻道尚未初始化AI聊天功能，請使用 `/init` 後再試");
                    return;
                }

                if (!channelConfig.IsEnable)
                {
                    await Context.Interaction.SendErrorAsync("本頻道已關閉AI聊天功能，請使用 `/toggle` 開啟後再試");
                    return;
                }
            }

            await DeferAsync();
            try
            {
                await _service.HandleAIChat(Context, message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HandleAIChat");
                await Context.Interaction.SendErrorAsync(ex.Message, true, true);
            }
        }

        [SlashCommand("reset", "重置對話紀錄")]
        [RequireContext(ContextType.Guild)]
        public async Task Reset()
        {
            _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
            _service.RefreshChannelConfig();
            await Context.Interaction.SendConfirmAsync("已重置歷史訊息");
        }
    }
}
