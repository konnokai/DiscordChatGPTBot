using Discord.Interactions;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DiscordChatGPTBot.Interaction.OpenAI.Service
{
    public class OpenAIService : IInteractionService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly ConcurrentDictionary<string, List<ChatPrompt>> _chatPrompt;
        private readonly ConcurrentDictionary<ulong, DateTime> _lastSendMessageTimestamp;
        private readonly ConcurrentDictionary<ulong, ushort> _turns;
        private readonly List<DataBase.Table.ChannelConfig> _channelConfigs = new List<DataBase.Table.ChannelConfig>();
        private readonly HashSet<ulong> _runningChannels = new HashSet<ulong>();

        public OpenAIService(BotConfig botConfig)
        {
            _openAIClient = new OpenAIClient(new OpenAIAuthentication(botConfig.OpenAIToken));
            _chatPrompt = new();
            _lastSendMessageTimestamp = new();
            _turns = new();

            RefreshChannelConfig();
        }

        public void RefreshChannelConfig()
        {
            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                _channelConfigs.Clear();
                _channelConfigs.AddRange(db.ChannelConfig);
            }
        }

        public async Task HandleAIChat(SocketInteractionContext context, string message)
        {
            if (_runningChannels.Contains(context.Channel.Id))
            {
                await context.Interaction.SendErrorAsync("還有回應尚未完成");
                return;
            }

            _runningChannels.Add(context.Channel.Id);

            await CheckReset(context);

            var msg = await context.Interaction.FollowupAsync("等待回應中...");

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

                        if (result.EndWithDelim())
                        {
                            try { await msg.ModifyAsync((act) => act.Content = result); }
                            catch { }
                        }
                    }

                    try { await msg.ModifyAsync((act) => act.Content = result); }
                    catch { }
                    _runningChannels.Remove(context.Channel.Id);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Talk Timeout");
                    await msg.ModifyAsync((act) => act.Content = "等待訊息失敗: 逾時");
                    _runningChannels.Remove(context.Channel.Id);
                }
            });

            var waitingTask = Task.Delay(TimeSpan.FromSeconds(10), cts2.Token);
            var completedTask = await Task.WhenAny(mainTask, waitingTask);
            if (completedTask == waitingTask && !waitingTask.IsCanceled)
            {
                // 如果等待Task先完成，就取消主要的Task
                cts.Cancel();
            }

            await mainTask;
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
                await context.Interaction.SendConfirmAsync("已重置歷史訊息", true);
            }
        }

        private async IAsyncEnumerable<string> ChatToAIAsync(ulong guildId, ulong channelId, ulong userId, string chat, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
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
