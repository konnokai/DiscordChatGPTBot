﻿using Discord.Commands;

namespace DiscordChatGPTBot.Command.Admin
{
    public class Administration : TopLevelModule<AdministrationService>
    {
        private readonly DiscordSocketClient _client;
        public Administration(DiscordSocketClient discordSocketClient)
        {
            _client = discordSocketClient;
        }

        [RequireContext(ContextType.DM)]
        [Command("UpdateStatus")]
        [Summary("更新機器人的狀態\n參數: Guild, Member, Count, Info")]
        [Alias("UpStats")]
        [RequireOwner]
        public async Task UpdateStatusAsync([Summary("狀態")] string stats)
        {
            switch (stats.ToLowerInvariant())
            {
                case "guild":
                    Program.Status = Program.BotPlayingStatus.Guild;
                    break;
                case "member":
                    Program.Status = Program.BotPlayingStatus.Member;
                    break;
                case "count":
                    Program.Status = Program.BotPlayingStatus.ExecutedTokenCount;
                    break;
                case "info":
                    Program.Status = Program.BotPlayingStatus.Info;
                    break;
                default:
                    await Context.Channel.SendConfirmAsync(string.Format("找不到 {0} 狀態", stats));
                    return;
            }
            Program.ChangeStatus();
            return;
        }

        [RequireContext(ContextType.DM)]
        [Command("ListServer")]
        [Summary("顯示所有的伺服器")]
        [Alias("LS")]
        [RequireOwner]
        public async Task ListServerAsync([Summary("頁數")] int page = 0)
        {
            await Context.SendPaginatedConfirmAsync(page, (cur) =>
            {
                EmbedBuilder embedBuilder = new EmbedBuilder().WithOkColor().WithTitle("目前所在的伺服器有");

                foreach (var item in _client.Guilds.Skip(cur * 5).Take(5))
                {
                    int totalMember = item.MemberCount;
                    bool isBotOwnerInGuild = item.GetUser(Program.ApplicatonOwner.Id) != null;

                    embedBuilder.AddField(item.Name, "Id: " + item.Id +
                        "\nOwner Id: " + item.OwnerId +
                        "\n人數: " + totalMember.ToString() +
                        "\nBot擁有者是否在該伺服器: " + (isBotOwnerInGuild ? "是" : "否"));
                }

                return embedBuilder;
            }, _client.Guilds.Count, 5);
        }

        [RequireContext(ContextType.DM)]
        [Command("SearchServer")]
        [Summary("查詢伺服器")]
        [Alias("SS")]
        [RequireOwner]
        public async Task SearchServerAsync([Summary("關鍵字")] string keyword = "", [Summary("頁數")] int page = 0)
        {
            if (ulong.TryParse(keyword, out ulong guildId))
            {
                var guild = _client.GetGuild(guildId);
                if (guild != null)
                {
                    bool isBotOwnerInGuild = guild.GetUser(Program.ApplicatonOwner.Id) != null;

                    var embed = new EmbedBuilder().WithOkColor().AddField(guild.Name,
                        $"Id: {guild.Id}\n" +
                        $"擁有者Id: {guild.OwnerId}\n" +
                        $"人數: {guild.MemberCount}\n").Build();

                    await Context.Channel.SendMessageAsync(embed: embed);
                    return;
                }
            }

            var list = _client.Guilds.Where((x) => x.Name.Contains(keyword, StringComparison.InvariantCultureIgnoreCase));
            if (list.Count() == 0)
            {
                await Context.Channel.SendErrorAsync("該關鍵字無伺服器");
                return;
            }

            await Context.SendPaginatedConfirmAsync(page, (cur) =>
            {
                EmbedBuilder embedBuilder = new EmbedBuilder().WithOkColor().WithTitle($"查詢 `{keyword}` 後的伺服器有");

                foreach (var item in list.Skip(cur * 5).Take(5))
                {
                    embedBuilder.AddField(item.Name,
                        $"Id: {item.Id}\n" +
                        $"擁有者Id: {item.OwnerId}\n" +
                        $"人數: {item.MemberCount}\n");
                }

                embedBuilder.WithFooter($"總數量: {list.Count()}");

                return embedBuilder;
            }, list.Count(), 5, false);
        }

        [RequireContext(ContextType.DM)]
        [Command("Die")]
        [Summary("關閉機器人")]
        [Alias("Bye")]
        [RequireOwner]
        public async Task DieAsync()
        {
            Program.isDisconnect = true;
            await Context.Channel.SendConfirmAsync("關閉中");
        }

        [RequireContext(ContextType.DM)]
        [Command("Leave")]
        [Summary("讓機器人離開指定的伺服器")]
        [RequireOwner]
        public async Task LeaveAsync([Summary("伺服器Id")] ulong gid = 0)
        {
            if (gid == 0) { await Context.Channel.SendErrorAsync("伺服器Id為空"); return; }

            var guild = _client.GetGuild(gid);
            if (guild == null)
                await Context.Channel.SendErrorAsync("伺服器不存在");

            try { await guild.LeaveAsync(); }
            catch (Exception) { await Context.Channel.SendErrorAsync("失敗，請確認Id是否正確"); return; }

            await Context.Channel.SendConfirmAsync("✅");
        }

        [RequireContext(ContextType.DM)]
        [Command("GetInviteURL")]
        [Summary("取得伺服器的邀請連結")]
        [Alias("invite")]
        [RequireOwner]
        public async Task GetInviteURLAsync([Summary("伺服器Id")] ulong gid = 0, [Summary("頻道Id")] ulong cid = 0)
        {
            if (gid == 0) gid = Context.Guild.Id;
            SocketGuild guild = _client.GetGuild(gid);
            if (guild == null)
            {
                await Context.Channel.SendErrorAsync($"伺服器 {gid} 不存在");
                return;
            }

            try
            {
                if (cid == 0)
                {
                    IReadOnlyCollection<SocketTextChannel> socketTextChannels = guild.TextChannels;

                    await Context.SendPaginatedConfirmAsync(0, (cur) =>
                    {
                        EmbedBuilder embedBuilder = new EmbedBuilder()
                           .WithOkColor()
                           .WithTitle("以下為 " + guild.Name + " 所有的文字頻道")
                           .WithDescription(string.Join('\n', socketTextChannels.Skip(cur * 10).Take(10).Select((x) => x.Id + " / " + x.Name)));

                        return embedBuilder;
                    }, socketTextChannels.Count, 10);
                }
                else
                {
                    IInviteMetadata invite = await guild.GetTextChannel(cid).CreateInviteAsync(300, 1, false);
                    await Context.Channel.SendConfirmAsync(invite.Url);
                }
            }
            catch (Discord.Net.HttpException httpEx)
            {
                if (httpEx.DiscordCode == DiscordErrorCode.InsufficientPermissions || httpEx.DiscordCode == DiscordErrorCode.MissingPermissions)
                    await Context.Channel.SendErrorAsync("缺少邀請權限");
                else
                    Log.Error(httpEx.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
    }
}