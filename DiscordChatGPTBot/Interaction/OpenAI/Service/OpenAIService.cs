using Discord.Interactions;
using DiscordChatGPTBot.Auth;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DiscordChatGPTBot.Interaction.OpenAI.Service
{
    public class OpenAIService : IInteractionService
    {
        private readonly ConcurrentDictionary<string, List<ChatPrompt>> _chatPrompt = new();
        private readonly ConcurrentDictionary<ulong, DateTime> _lastSendMessageTimestamp = new();
        private readonly ConcurrentDictionary<ulong, ushort> _turns = new();
        private readonly ConcurrentDictionary<ulong, string> _guildOpenAIKey = new();
        private readonly List<DataBase.Table.ChannelConfig> _channelConfigs = new();
        private readonly HashSet<ulong> _runningChannels = new();
        private readonly BotConfig _botConfig;

        public OpenAIService(BotConfig botConfig)
        {
            _botConfig = botConfig;
            RefreshChannelConfig();
            RefreshGuildConfig();
        }

        public void RefreshChannelConfig()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                _channelConfigs.Clear();
                _channelConfigs.AddRange(db.ChannelConfig);                
            }
        }

        public void RefreshGuildConfig()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                _guildOpenAIKey.Clear();
                foreach (var item in db.GuildConfig)
                {
                    _guildOpenAIKey.AddOrUpdate(item.GuildId, item.OpenAIKey, (guildId, apiKey) => item.OpenAIKey);
                }
            }
        }

        public async Task HandleAIChat(SocketInteractionContext context, string message)
        {
            if (_runningChannels.Contains(context.Channel.Id))
            {
                await context.Interaction.SendErrorAsync("還有回應尚未完成", true);
                return;
            }

            _runningChannels.Add(context.Channel.Id);

            await CheckReset(context);

            var msg = await context.Interaction.FollowupAsync("等待回應中...");

            do
            {
                var cts = new CancellationTokenSource();
                var cts2 = new CancellationTokenSource();

                var mainTask = Task.Run(async () =>
                {
                    try
                    {
                        string result = "";
                        await foreach (var item in ChatToAIAsync(context.Guild.Id, context.Channel.Id, context.User.Id, message, cts.Token))
                        {
                            result += item;
                            if (!cts2.IsCancellationRequested) cts2.Cancel();

                            result = result.Replace("\n\n", "\n");
                            if (result.EndWithDelim())
                            {
                                try { await msg.ModifyAsync((act) => act.Content = result); }
                                catch { }
                            }
                        }

                        try { await msg.ModifyAsync((act) => act.Content = result); }
                        catch { }

                        Log.New($"回應: {result}");
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "HandleAIChat");
                    }
                });

                var waitingTask = Task.Delay(TimeSpan.FromSeconds(3), cts2.Token);
                var completedTask = await Task.WhenAny(mainTask, waitingTask);
                if (completedTask == waitingTask && !waitingTask.IsCanceled)
                {
                    // 如果等待Task先完成，就取消主要的Task
                    cts.Cancel();

                    await msg.ModifyAsync((act) => act.Content = "等待訊息逾時，嘗試重新讀取中...");
                    await Task.Delay(3000);
                    await msg.ModifyAsync((act) => act.Content = "等待回應中...");
                }
                else
                {
                    break;
                }

                await mainTask;
            } while (true);

            _runningChannels.Remove(context.Channel.Id);
            _turns.AddOrUpdate(context.Channel.Id, 1, (channelId, turn) => turn++);
        }

        public void ForceReset(ulong guildId, ulong channelId)
        {
            _runningChannels.Remove(channelId);
            _lastSendMessageTimestamp.AddOrUpdate(channelId, (channelId) => DateTime.Now, (channelId, dataTime) => DateTime.Now);
            _chatPrompt.TryRemove($"{channelId}", out var _);
            _turns.TryRemove(channelId, out var _);
        }

        private async Task CheckReset(SocketInteractionContext context)
        {
            var channelConfig = _channelConfigs.SingleOrDefault((x) => x.GuildId == context.Guild.Id && x.ChannelId == context.Channel.Id) ?? throw new InvalidOperationException("資料庫無此頻道的資料");
            var dateTime = _lastSendMessageTimestamp.GetOrAdd(context.Channel.Id, DateTime.Now);
            bool isTurnsMax = _turns.ContainsKey(context.Channel.Id) && _turns[context.Channel.Id] > channelConfig.MaxTurns;
            bool isNeedResetTime = DateTime.Now.Subtract(dateTime).TotalSeconds > channelConfig.ResetDeltaTime;

            if (isTurnsMax || isNeedResetTime)
            {
                _chatPrompt.TryRemove($"{context.Channel.Id}", out var _);
                _turns.TryRemove(context.Channel.Id, out var _);
                _lastSendMessageTimestamp.AddOrUpdate(context.Channel.Id, (channelId) => DateTime.Now, (channelId, dataTime) => DateTime.Now);
                await context.Interaction.SendConfirmAsync("已重置歷史訊息", true);
            }
        }

        private async IAsyncEnumerable<string> ChatToAIAsync(ulong guildId, ulong channelId, ulong userId, string chat, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_guildOpenAIKey.TryGetValue(guildId, out string? apiKey))
                throw new InvalidOperationException("APIKey未設置");

            string desKey = TokenManager.GetTokenValue(apiKey, _botConfig.AESKey);
            if (string.IsNullOrEmpty(desKey) || desKey.Length != 51)
                throw new InvalidOperationException("APIKey解密失敗");

            OpenAIClient _openAIClient = new OpenAIClient(desKey);

            var chatPrompts = GetOrAddChatPrompt(channelId);
            chatPrompts.AddChat("user", chat);

            var chatRequest = new ChatRequest(chatPrompts, Model.GPT3_5_Turbo);
            string completionMessage = "";
            string role = "";

            await foreach (var result in _openAIClient.ChatEndpoint.StreamCompletionEnumerableAsync(chatRequest, cancellationToken))
            {
                Log.Debug(result.ToString());

                if (result?.FirstChoice?.FinishReason != null && result?.FirstChoice?.FinishReason == "stop")
                    break;

                if (result?.FirstChoice?.Delta == null)
                    continue;

                if (result.FirstChoice.Delta.Role != null)
                    role = result.FirstChoice.Delta.Role;

                if (result.FirstChoice.Delta.Content != null)
                {
                    completionMessage += result.FirstChoice.Delta.Content;
                    yield return result.FirstChoice.Delta.Content;
                }

                //result.Usage.TotalTokens
            }

            chatPrompts.AddChat(role, completionMessage);
            _lastSendMessageTimestamp.AddOrUpdate(channelId, (channelId) => DateTime.Now, (channelId, dataTime) => DateTime.Now);

            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                db.ChatHistroy.Add(new DataBase.Table.ChatHistroy() { GuildId = guildId, ChannelId = channelId, UserId = userId, SystemPrompt = chatPrompts.First().Content, UserPrompt = chat });
                db.SaveChanges();
            }
        }

        private List<ChatPrompt> GetOrAddChatPrompt(ulong channelId)
        {
            var channelConfig = _channelConfigs.SingleOrDefault((x) => x.ChannelId == channelId);
            return channelConfig == null
                ? throw new InvalidOperationException("資料庫無此頻道的資料")
                : _chatPrompt.GetOrAdd($"{channelId}", (key) => new List<ChatPrompt>() { new ChatPrompt("system", channelConfig.SystemPrompt) });
        }
    }

    static class Ext
    {
        private static readonly string[] Delims = new string[] { "\n", ",", ".", "，", "。", "、", "：", "（", "）", "「", "」" };

        public static void AddChat(this List<ChatPrompt> chatPrompts, string role, string chat)
        {
            chatPrompts.Add(new ChatPrompt(role, chat));
        }

        public static bool EndWithDelim(this string context)
        {
            foreach (var delim in Delims)
            {
                if (context.EndsWith(delim))
                    return true;
            }

            return false;
        }
    }
}
