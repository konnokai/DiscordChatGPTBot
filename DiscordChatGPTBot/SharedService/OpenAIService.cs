using DiscordChatGPTBot.Auth;
using DiscordChatGPTBot.Interaction;
using OpenAI;
using OpenAI.Chat;
using SharpToken;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Model = OpenAI.Models.Model;

namespace DiscordChatGPTBot.SharedService.OpenAI
{
    public class OpenAIService : IInteractionService
    {
        private readonly ConcurrentDictionary<string, List<Message>> _chatPrompt = new();
        private readonly ConcurrentDictionary<ulong, DateTime> _lastSendMessageTimestamp = new();
        private readonly ConcurrentDictionary<ulong, string> _guildOpenAIKey = new();
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _cancellationTokenSource = new();
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

        public bool IsRunningAIChat(ulong channelId)
            => _runningChannels.Contains(channelId);

        public bool StopChat(ulong channelId)
        {
            if (_cancellationTokenSource.TryRemove(channelId, out var cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
                return true;
            }

            return false;
        }

        public async Task HandleAIChat(ulong guildId, ISocketMessageChannel channel, ulong userId, string message)
        {
            try
            {
                if (_runningChannels.Contains(channel.Id))
                {
                    await channel.SendMessageAsync("還有回應尚未完成");
                    return;
                }

                _runningChannels.Add(channel.Id);

                await CheckReset(guildId, channel);

                var msg = await channel.SendMessageAsync("等待回應中...");
                bool isResponed = false;

                do
                {
                    var cts = _cancellationTokenSource.AddOrUpdate(channel.Id, (channeId) => { return new CancellationTokenSource(); }, (channelId, oldCts) => { return new CancellationTokenSource(); });
                    var cts2 = new CancellationTokenSource();
                    string result = "";

                    var waitingTask = Task.Delay(TimeSpan.FromSeconds(3), cts2.Token);

                    var mainTask = Task.Run(async () =>
                    {
                        try
                        {
                            int wordCount = 0;
                            await foreach (var item in ChatToAIAsync(guildId, channel.Id, userId, message, cts.Token))
                            {
                                wordCount++;
                                result += item;
                                if (!cts2.IsCancellationRequested) cts2.Cancel();

                                result = result.Replace("\n\n", "\n");
                                if (result.EndWithDelim() || wordCount >= 100)
                                {
                                    wordCount = 0;
                                    try { await msg.ModifyAsync((act) => act.Content = result); }
                                    catch { }
                                }
                            }

                            try { await msg.ModifyAsync((act) => act.Content = result); }
                            catch { }

                            Log.New($"回應: {result}");
                            isResponed = true;
                        }
                        catch (TaskCanceledException) { }
                        catch (OperationCanceledException) when (waitingTask.IsCanceled)
                        {
                            try { await msg.ModifyAsync((act) => act.Content = result); }
                            catch { }
                            Log.New($"回應: {result}");
                            isResponed = true;
                        }
                        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            string message = GetOpenAIErrorMessage("已達到ChatGPT請求上限，請稍後再試", httpEx.Message);
                            await msg.ModifyAsync((act) => act.Content = message);
                            Log.Error("HandleAIChat-429 Error");
                            isResponed = true;
                        }
                        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.BadRequest)
                        {
                            string message = GetOpenAIErrorMessage("錯誤的請求，可能是已達到ChatGPT的Token上限，請嘗試 `/reset` 後重新發言", httpEx.Message);
                            await msg.ModifyAsync((act) => act.Content = message);
                            Log.Error("HandleAIChat-400 Error");
                            Log.Error(message);
                            isResponed = true;
                        }
                        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                        {
                            string message = GetOpenAIErrorMessage("ChatGPT伺服器出現問題，請稍後再試", httpEx.Message);
                            await msg.ModifyAsync((act) => act.Content = message);
                            Log.Error("HandleAIChat-500 Error");
                            isResponed = true;
                        }
                        catch (IOException ioEx) when (ioEx.Message.Contains("The response ended prematurely")) { } // 忘記這是幹嘛的
                        catch (Exception ex)
                        {
                            await msg.ModifyAsync((act) =>
                            act.Content = $"出現錯誤，請向Bot擁有者確認\n" +
                                          $"```\n" +
                                          $"{ex.Message}\n" +
                                          $"```");
                            Log.Error(ex, "HandleAIChat");
                            isResponed = true;
                        }
                    });

                    var completedTask = await Task.WhenAny(mainTask, waitingTask);
                    if (completedTask == waitingTask && !waitingTask.IsCanceled)
                    {
                        // 如果等待Task先完成，就取消主要的Task
                        cts.Cancel();

                        await msg.ModifyAsync((act) => act.Content = "等待訊息逾時，嘗試重新讀取中...");
                        await Task.Delay(3000);
                        await msg.ModifyAsync((act) => act.Content = "等待回應中...");
                    }

                    await mainTask;
                } while (!isResponed);

                IEmote? iEmote = null;
                if (Emote.TryParse(_channelConfigs.Single((x) => x.ChannelId == channel.Id).CompletedEmoji, out var emote))
                    iEmote = emote;
                else if (Emoji.TryParse(_channelConfigs.Single((x) => x.ChannelId == channel.Id).CompletedEmoji, out var emoji))
                    iEmote = emoji;

                try
                {
                    if (iEmote != null)
                        await msg.AddReactionAsync(iEmote, new RequestOptions() { RetryMode = RetryMode.AlwaysRetry });
                }
                catch (Discord.Net.HttpException httpEx) when (httpEx.DiscordCode == DiscordErrorCode.UnknownEmoji)
                {
                    await channel.SendMessageAsync("完成表情遺失，請使用 `/set-complete-emote` 重新設定");
                    await msg.AddReactionAsync(Emoji.Parse(":ok:"), new RequestOptions() { RetryMode = RetryMode.AlwaysRetry });
                }

                _cancellationTokenSource.TryRemove(channel.Id, out var _);
            }
            catch (Exception)
            {
                throw;
            }
            // ChatGPT: 不管是否有發生例外，finally 區塊中的程式碼都會被執行，包括在catch區塊中使用throw語句重新拋出異常的情況下。
            // 這是因為finally區塊是在 try-catch 區塊之後、最後執行的一個區塊，無論有沒有出現異常，都需要執行 finally 區塊以保證相關資源的正確釋放。 
            finally
            {
                _runningChannels.Remove(channel.Id);
            }
        }

        public void ForceReset(ulong guildId, ulong channelId)
        {
            _runningChannels.Remove(channelId);
            _lastSendMessageTimestamp.AddOrUpdate(channelId, (channelId) => DateTime.Now, (channelId, dataTime) => DateTime.Now);
            _chatPrompt.TryRemove($"{channelId}", out var _);
        }

        private string GetOpenAIErrorMessage(string message, string errorMessage)
        {
            // Create by ChatGPT
            Regex regex = new Regex(@"""message"":\s*""(.*?)""");
            var match = regex.Match(errorMessage);
            if (match.Success)
                message += $"\nChatGPT回傳訊息:\n" +
                    $"```\n" +
                    $"{match.Groups[1].Value}\n" +
                    $"```";

            return message;
        }

        private async Task CheckReset(ulong guildId, ISocketMessageChannel channel)
        {
            var channelConfig = _channelConfigs.SingleOrDefault((x) => x.GuildId == guildId && x.ChannelId == channel.Id) ?? throw new InvalidOperationException("資料庫無此頻道的資料");
            if (!channelConfig.IsEnable) throw new InvalidOperationException("本頻道已關閉ChatGPT聊天功能，請管理員使用 `/toggle` 開啟後再試");

            var assistantPromptCount = GetOrAddChatPrompt(channel.Id).Count((x) => x.Role == Role.Assistant);
            bool isTurnsMax = assistantPromptCount >= channelConfig.MaxTurns;

            var dateTime = _lastSendMessageTimestamp.GetOrAdd(channel.Id, DateTime.Now);
            bool isNeedResetTime = DateTime.Now.Subtract(dateTime).TotalSeconds > channelConfig.ResetDeltaTime;

            if (assistantPromptCount > 0) Log.Debug($"{channel.Id} ({dateTime}): {assistantPromptCount}");
            else Log.Debug($"{channel.Id} ({dateTime}): First Turn");

            if (isTurnsMax || isNeedResetTime)
            {
                _lastSendMessageTimestamp.AddOrUpdate(channel.Id, (channelId) => DateTime.Now, (channelId, dataTime) => DateTime.Now);

                if (channelConfig.IsInheritChatWhenReset)
                {
                    var tempChatPrompt = GetOrAddChatPrompt(channel.Id).TakeLast(6).Where((x) => x.Role != Role.System);
                    _chatPrompt.TryRemove($"{channel.Id}", out var _);
                    var chatPrompts = GetOrAddChatPrompt(channel.Id);
                    chatPrompts.AddRange(tempChatPrompt);
                    await channel.SendMessageAsync("已重置並繼承歷史訊息");
                }
                else
                {
                    _chatPrompt.TryRemove($"{channel.Id}", out var _);
                    await channel.SendMessageAsync("已重置歷史訊息");
                }
            }
        }

        private async IAsyncEnumerable<string> ChatToAIAsync(ulong guildId, ulong channelId, ulong userId, string chat, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_guildOpenAIKey.TryGetValue(guildId, out string? apiKey))
                throw new InvalidOperationException("APIKey未設置");

            string desKey;
            try
            {
                desKey = TokenManager.GetTokenValue(apiKey, _botConfig.AESKey);
                if (string.IsNullOrEmpty(desKey) || desKey.Length != 51)
                    throw new InvalidOperationException("APIKey解密失敗");
            }
            catch
            {
                throw new InvalidOperationException("APIKey解密失敗");
            }

            // https://blog.miniasp.com/post/2023/09/22/Use-SharpToken-to-count-number-of-tokens
            var encoding = GptEncoding.GetEncoding("cl100k_base");
            OpenAIClient _openAIClient = new OpenAIClient(desKey);

            var chatPrompts = GetOrAddChatPrompt(channelId);
            var systemTokenCount = encoding.Encode(chatPrompts.First().Content).Count;
            Log.Debug($"systemTokenCount: {systemTokenCount}");
            chatPrompts.AddChat(Role.User, chat);

            var chatRequest = new ChatRequest(chatPrompts, Model.GPT3_5_Turbo, user: $"{guildId}-{channelId}-{userId}");
            string completionMessage = "";

            await foreach (var result in _openAIClient.ChatEndpoint.StreamCompletionEnumerableAsync(chatRequest, cancellationToken))
            {
                Log.Debug(JsonConvert.SerializeObject(result));

                if (result?.FirstChoice?.FinishReason != null && result?.FirstChoice?.FinishReason == "stop")
                    break;

                if (result?.FirstChoice?.Delta == null)
                    continue;

                if (result.FirstChoice.Delta.Content != null)
                {
                    completionMessage += result.FirstChoice.Delta.Content;
                    yield return result.FirstChoice.Delta.Content;
                }
            }

            var chatTokenCount = chatPrompts.Sum((x) => encoding.Encode(x.Content).Count) + 11; // 不知道為何會缺少 11 Token
            var completionTokenCount = encoding.Encode(completionMessage).Count;

            chatPrompts.AddChat(Role.Assistant, completionMessage);
            _lastSendMessageTimestamp.AddOrUpdate(channelId, (channelId) => DateTime.Now, (channelId, dataTime) => DateTime.Now);

            Log.Debug($"chatTokenCount: {chatTokenCount}, completionTokenCount: {completionTokenCount}, totalTokenCount: {chatTokenCount + completionTokenCount}");

            using (var db = DataBase.MainDbContext.GetDbContext())
            {
                db.ChatHistroy.Add(new DataBase.Table.ChatHistroy()
                {
                    GuildId = guildId,
                    ChannelId = channelId,
                    UserId = userId,
                    SystemPrompt = chatPrompts.First().Content,
                    UserPrompt = chat,
                    ChatUseTokenCount = chatTokenCount,
                    ResultUseTokenCount = completionTokenCount,
                    TotlaUseTokenCount = chatTokenCount + completionTokenCount
                });
                db.SaveChanges();
            }
        }

        private List<Message> GetOrAddChatPrompt(ulong channelId)
        {
            var channelConfig = _channelConfigs.SingleOrDefault((x) => x.ChannelId == channelId);
            return channelConfig == null
                ? throw new InvalidOperationException("資料庫無此頻道的資料")
                : _chatPrompt.GetOrAdd($"{channelId}", (key) => new List<Message>() { new Message(Role.System, channelConfig.SystemPrompt) });
        }
    }

    static class Ext
    {
        private static readonly string[] Delims = new string[] { "\n", ",", ".", "，", "。", "、", "：", "）", "」", "}", "]" };

        public static void AddChat(this List<Message> chatPrompts, Role role, string chat)
        {
            chatPrompts.Add(new Message(role, chat));
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
