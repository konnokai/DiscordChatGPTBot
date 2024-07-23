using Discord.Interactions;
using DiscordChatGPTBot.Auth;
using DiscordChatGPTBot.DataBase.Table;

namespace DiscordChatGPTBot.Interaction.OpenAI
{
    [CommandContextType(InteractionContextType.Guild)]
    public class OpenAI : TopLevelModule<SharedService.OpenAI.OpenAIService>
    {
        public enum ToggleSetting
        {
            [ChoiceDisplay("聊天開關")]
            Enable,
            [ChoiceDisplay("重置紀錄時繼承最後的聊天")]
            InheritChatWhenReset
        }

        private readonly DiscordSocketClient _client;
        private readonly BotConfig _botConfig;

        public OpenAI(DiscordSocketClient client, BotConfig botConfig)
        {
            _client = client;
            _botConfig = botConfig;
        }

        private async Task<GuildConfig?> EnsureGuildIsInitAndGetConfigAsync(bool sendErrorMsg = true)
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var guildConfig = db.GuildConfig.SingleOrDefault((x) => x.GuildId == Context.Guild.Id);
                if (guildConfig == null && sendErrorMsg)
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
                    await Context.Interaction.SendErrorAsync("本頻道尚未啟 ChatGPT 聊天功能，請使用 `/active` 後再試");

                return channelConfig;
            }
        }

        [SlashCommand("init", "初始化或更新本伺服器的 OpenAI API 設定")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Initialization([Summary("api-key", "OpenAI 的 API Key")] string apiKey)
        {
            if (!apiKey.StartsWith("sk-") || apiKey.Length != 51)
            {
                await Context.Interaction.SendErrorAsync("OpenAI API Key 格式錯誤，請輸入正確的 API Key");
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

                await Context.Interaction.SendConfirmAsync("已更新 OpenAI API Key", false, true);
            }
        }

        [SlashCommand("revoke", "撤銷本伺服器的 OpenAI API Key 並取消使用 ChatGPT 聊天功能")]
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

                await DeferAsync(true);

                if (await PromptUserConfirmAsync("撤銷 API Key 將會連同移除本伺服器的 ChatGPT 聊天設定，是否繼續?"))
                {
                    db.GuildConfig.Remove(guildConfig);
                    db.ChannelConfig.RemoveRange(db.ChannelConfig.Where((x) => x.GuildId == Context.Guild.Id));
                    db.SaveChanges();

                    _service.RefreshGuildConfig();

                    await Context.Interaction.SendConfirmAsync("已撤銷", true, true);
                }
            }
        }

        [SlashCommand("active", "在此頻道啟用 ChatGPT 聊天功能")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Active([Summary("人設", "可使用 \"/set-system-prompt\" 變更")] string prompt = "你是一個有幫助的助手。使用繁體中文回答問題。")
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var guildConfig = await EnsureGuildIsInitAndGetConfigAsync();
                if (guildConfig == null)
                    return;

                if (Context.Channel is not IGuildChannel channel)
                    return;

                var permissions = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(channel);
                if (!permissions.ViewChannel || !permissions.SendMessages)
                {
                    await Context.Interaction.SendErrorAsync($"我在 `{channel}` 沒有 `讀取&編輯頻道` 的權限，請給予權限後再次執行本指令");
                    return;
                }

                if (!permissions.EmbedLinks)
                {
                    await Context.Interaction.SendErrorAsync($"我在 `{channel}` 沒有 `嵌入連結` 的權限，請給予權限後再次執行本指令");
                    return;
                }

                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync(false);
                if (channelConfig != null)
                {
                    await Context.Interaction.SendErrorAsync("本頻道已啟用\n" +
                        $"如需更改人設請使用 `/set-system-prompt`\n" +
                        $"如需更改完成表情請使用 `/set-complete-emote`\n" +
                        $"如需更改模型請使用 `/set-chatgpt-model`\n" +
                        $"如需切換開關請使用 `/toggle`");
                    return;
                }

                db.ChannelConfig.Add(new ChannelConfig() { GuildId = Context.Guild.Id, ChannelId = Context.Channel.Id, SystemPrompt = prompt });
                db.SaveChanges();

                _service.RefreshChannelConfig();

                await Context.Interaction.SendConfirmAsync($"已在此頻道啟用 ChatGPT 對話功能\n" +
                    $"如需更改人設請使用 `/set-system-prompt`\n" +
                    $"如需更改完成表情請使用 `/set-complete-emote`\n" +
                    $"如需更改模型請使用 `/set-chatgpt-model`\n" +
                    $"如需切換開關請使用 `/toggle`\n" +
                    $"ChatGPT人設:\n" +
                    $"```\n" +
                    $"{prompt}\n" +
                    $"```");
            }
        }

        [SlashCommand("set-system-prompt", "設定 ChatGPT 的人設")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSystemPrompt([Summary("人設", "預設值: 你是一個有幫助的助手。使用正體中文回答問題。")] string prompt = "你是一個有幫助的助手。使用正體中文回答問題。")
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                channelConfig.SystemPrompt = prompt;
                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync("已更新 ChatGPT 人設:\n" +
                    $"```\n" +
                    $"{prompt}\n" +
                    $"```");

                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("set-chatgpt-model", "設定 ChatGPT 的模型")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetChatGPTModel([Summary("模型", "預設使用 GPT-4o Mini")] ChannelConfig.ChatGPTModel chatGPTModel = ChannelConfig.ChatGPTModel.GPT4o_Mini)
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                channelConfig.UsedChatGPTModel = chatGPTModel;
                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync($"已更新此頻道所使用的 ChatGPT 模型為 `{chatGPTModel}`");

                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("set-complete-emote", "設定回應完成時的表情")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCompletedEmote([Summary("表情", "可使用本伺服器表情或 Discord 內建表情")] string emoteName)
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                emoteName = emoteName.Trim();
                IEmote? emote = null;
                if (Emoji.TryParse(emoteName, out var result))
                    emote = result;
                else if (Emote.TryParse(emoteName, out var result1))
                {
                    if (!Context.Guild.Emotes.Any((x) => x.Id == result1.Id))
                    {
                        await Context.Interaction.SendErrorAsync("僅可新增本伺服器的表情");
                        return;
                    }
                    emote = result1;
                }

                if (emote == null)
                {
                    await Context.Interaction.SendErrorAsync("表情辨識失敗，僅可使用一個表情");
                    return;
                }

                channelConfig.CompletedEmoji = emote.ToString()!;
                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync($"已更新回應完成時的表情: {emote}");

                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("show-system-prompt", "顯示 ChatGPT 的人設")]
        [RequireContext(ContextType.Guild)]
        public async Task ShowSystemPrompt()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                await Context.Interaction.SendConfirmAsync("本頻道的 ChatGPT 人設:\n" +
                    $"```\n" +
                    $"{channelConfig.SystemPrompt}\n" +
                    $"```");
            }
        }

        [SlashCommand("toggle", "切換開關設定")]
        [RequireContext(ContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Toggle([Summary("setting", "設定")] ToggleSetting toggleSetting)
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                var channelConfig = await EnsureChannelIsActiveAndGetConfigAsync();
                if (channelConfig == null)
                    return;

                switch (toggleSetting)
                {
                    case ToggleSetting.Enable:
                        channelConfig.IsEnable = !channelConfig.IsEnable;
                        break;
                    case ToggleSetting.InheritChatWhenReset:
                        channelConfig.IsInheritChatWhenReset = !channelConfig.IsInheritChatWhenReset;
                        break;
                }

                db.ChannelConfig.Update(channelConfig);
                db.SaveChanges();

                await Context.Interaction.SendConfirmAsync("設定 ChatGPT 聊天功能為: " + (channelConfig.IsEnable ? "開啟" : "關閉") +
                    "\n當重置時繼承最後三次的對話: " + (channelConfig.IsInheritChatWhenReset ? "開啟" : "關閉"));
                _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
                _service.RefreshChannelConfig();
            }
        }

        [SlashCommand("say", "跟 ChatGPT 對話")]
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
                    await Context.Interaction.SendErrorAsync("本頻道已關閉 ChatGPT 聊天功能，請管理員使用 `/toggle` 開啟後再試", true);
                    return;
                }
            }

            try
            {
                await Context.Interaction.SendConfirmAsync($"{Context.User}: {message}");
                await _service.HandleAIChat(Context.Guild.Id, Context.Channel, Context.User.Id, message);
            }
            catch (Discord.Net.HttpException httpEx) when (httpEx.DiscordCode == DiscordErrorCode.MissingPermissions)
            {
                Log.Warn($"{Context.Guild}/{Context.Channel} 缺少權限");
                await Context.Interaction.SendErrorAsync($"我在 `{Context.Channel}` 沒有 `讀取&編輯頻道` 的權限，請給予權限後再次執行本指令", true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HandleAIChat");
                await Context.Interaction.SendErrorAsync(ex.Message, true, true);
            }
        }

        [SlashCommand("stop", "停止對話")]
        [RequireContext(ContextType.Guild)]
        public async Task Stop()
        {
            if (!_service.IsRunningAIChat(Context.Channel.Id))
            {
                await Context.Interaction.SendErrorAsync("目前沒有正在回應的訊息");
                return;
            }

            if (_service.StopChat(Context.Channel.Id))
                await Context.Interaction.SendConfirmAsync("已取消");
            else
                await Context.Interaction.SendErrorAsync("無法取消");
        }

        [SlashCommand("reset", "重置對話紀錄")]
        [RequireContext(ContextType.Guild)]
        public async Task Reset()
        {
            if (_service.IsRunningAIChat(Context.Channel.Id))
            {
                await Context.Interaction.SendErrorAsync("還有回應尚未完成");
                return;
            }

            _service.ForceReset(Context.Guild.Id, Context.Channel.Id);
            _service.RefreshChannelConfig();
            await Context.Interaction.SendConfirmAsync("已重置歷史訊息");
        }
    }
}
