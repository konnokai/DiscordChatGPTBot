using Discord.Interactions;
using DiscordChatGPTBot.Auth;
using DiscordChatGPTBot.DataBase.Table;

namespace DiscordChatGPTBot.Interaction.OpenAI
{
    public class OpenAI : TopLevelModule<SharedService.OpenAI.OpenAIService>
    {
        private readonly BotConfig _botConfig;

        public OpenAI(BotConfig botConfig)
        {
            _botConfig = botConfig;
        }

        private async Task<GuildConfig?> EnsureGuildIsInitAndGetConfigAsync()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var guildConfig = db.GuildConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id);
                if (guildConfig == null)
                    await Context.Interaction.SendErrorAsync("本伺服器未初始化，請使用 `/init` 後再試");

                return guildConfig;
            }
        }

        private async Task<ChannelConfig?> EnsureChannelIsActiveAndGetConfigAsync(bool sendErrorMsg = true)
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                ChannelConfig? channelConfig = db.ChannelConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id && x.ChannelId == Context.Channel.Id);
                if (channelConfig == null && sendErrorMsg)
                    await Context.Interaction.SendErrorAsync("本頻道尚未啟ChatGPT聊天功能，請使用 `/active` 後再試");

                return channelConfig;
            }
        }

        [SlashCommand("init", "初始化或更新本伺服器的OpenAI API設定")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Initialization([Summary("open-ai-api-key", "OpenAI的API Key")] string apiKey)
        {
            if (!apiKey.StartsWith("sk-") || apiKey.Length != 51)
            {
                await Context.Interaction.SendErrorAsync("OpenAI API Key格式錯誤，請輸入正確的API Key", false, true);
                return;
            }

            string encToken = TokenManager.CreateToken(apiKey, _botConfig.AESKey);

            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var guildConfig = db.GuildConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id);
                if (guildConfig == null)
                {
                    guildConfig = new GuildConfig() { GuildId = Context.Guild.Id, OpenAIKey = encToken };
                    db.GuildConfig.Add(guildConfig);
                }
                else
                {
                    guildConfig.OpenAIKey = encToken;
                    db.GuildConfig.Update(guildConfig);
                }

                db.SaveChanges();
                _service.RefreshGuildConfig();

                await Context.Interaction.SendConfirmAsync("已更新OpenAI API Key", false, true);
            }
        }

        [SlashCommand("revoke", "撤銷本伺服器的OpenAI API Key並取消使用ChatGPT聊天功能")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Revoke()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var guildConfig = await EnsureGuildIsInitAndGetConfigAsync();
                if (guildConfig == null)
                    return;

                if (await PromptUserConfirmAsync("撤銷API Key將會連同移除本伺服器的ChatGPT聊天設定，是否繼續?"))
                {
                    db.GuildConfig.Remove(guildConfig);
                    db.ChannelConfig.RemoveRange(db.ChannelConfig.Where((x) => x.GuildId == Context.Guild.Id));
                    db.SaveChanges();

                    _service.RefreshGuildConfig();

                    await Context.Interaction.SendConfirmAsync("已撤銷", true);
                }
            }
        }

        [SlashCommand("active", "在此頻道啟用ChatGPT聊天功能")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Active([Summary("system-prompt", "人設，可使用 \"/set-system-prompt\" 變更")] string prompt = "你是一個有幫助的助手。使用繁體中文回答問題。")
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var guildConfig = await EnsureGuildIsInitAndGetConfigAsync();
                if (guildConfig == null)
                    return;

                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync(false);
                if (channelConfig != null)
                {
                    await Context.Interaction.SendErrorAsync("本頻道已啟用\n" +
                        $"如需更改人設請使用 `/set-system-prompt`\n" +
                        $"如需關閉請使用 `/toggle`");
                    return;
                }

                db.ChannelConfig.Add(new ChannelConfig() { GuildId = Context.Guild.Id, ChannelId = Context.Channel.Id, SystemPrompt = prompt });
                db.SaveChanges();

                _service.RefreshChannelConfig();

                await Context.Interaction.SendConfirmAsync($"已在此頻道啟用ChatGPT對話功能\n" +
                    $"如需更改人設請使用 `/set-system-prompt`\n" +
                    $"如需關閉請使用 `/toggle`\n" +
                    $"ChatGPT人設:\n" +
                    $"```\n" +
                    $"{prompt}\n" +
                    $"```");
            }
        }

        [SlashCommand("set-system-prompt", "設定ChatGPT的人設")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSystemPrompt([Summary("system-prompt", "人設，預設值: 你是一個有幫助的助手。使用繁體中文回答問題。")] string prompt = "你是一個有幫助的助手。使用繁體中文回答問題。")
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                channelConfig.SystemPrompt = prompt;
                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync("已更新ChatGPT人設:\n" +
                    $"```\n" +
                    $"{prompt}\n" +
                    $"```");

                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("show-system-prompt", "顯示ChatGPT的人設")]
        [RequireContext(ContextType.Guild)]
        public async Task ShowSystemPrompt()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                await Context.Interaction.SendConfirmAsync("本頻道的ChatGPT人設:\n" +
                    $"```\n" +
                    $"{channelConfig.SystemPrompt}\n" +
                    $"```");
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
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                channelConfig.IsEnable = !channelConfig.IsEnable;
                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync("已切換ChatGPT聊天功能為: " + (channelConfig.IsEnable ? "開啟" : "關閉"));
                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("say", "跟ChatGPT對話")]
        [RequireContext(ContextType.Guild)]
        public async Task Say(string message)
        {
            ChannelConfig? channelConfig;
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                if (!channelConfig.IsEnable)
                {
                    await Context.Interaction.SendErrorAsync("本頻道已關閉ChatGPT聊天功能，請使用 `/toggle` 開啟後再試");
                    return;
                }
            }

            try
            {
                await Context.Interaction.SendConfirmAsync($"{Context.User}: {message}");
                await _service.HandleAIChat(Context.Guild.Id, Context.Channel, Context.User.Id, message);
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
