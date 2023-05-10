using Discord.Commands;
using System.Reflection;

namespace DiscordChatGPTBot.Command
{
    class CommandHandler : ICommandService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly SharedService.OpenAI.OpenAIService _openAIService;

        public CommandHandler(IServiceProvider services, CommandService commands, DiscordSocketClient client, SharedService.OpenAI.OpenAIService openAIService)
        {
            _commands = commands;
            _services = services;
            _client = client;
            _openAIService = openAIService;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(
                assembly: Assembly.GetEntryAssembly(),
                services: _services);
            _client.MessageReceived += (msg) => { var _ = Task.Run(() => HandleCommandAsync(msg)); return Task.CompletedTask; };
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            var message = messageParam as SocketUserMessage;
            if (message == null || message.Author.IsBot) return;

            int argPos = 0;
            if (message.HasStringPrefix("c!", ref argPos))
            {
                var context = new SocketCommandContext(_client, message);

                if (_commands.Search(context, argPos).IsSuccess)
                {
                    var result = await _commands.ExecuteAsync(
                        context: context,
                        argPos: argPos,
                        services: _services);

                    if (!result.IsSuccess)
                    {
                        Log.Error($"[{context.Guild?.Name}/{context.Message.Channel?.Name}] {message.Author.Username} 執行 {context.Message} 發生錯誤");
                        Log.Error(result.ErrorReason);
                        await context.Channel.SendErrorAsync(result.ErrorReason);
                    }
                    else
                    {
                        try { if (context.Message.Author.Id == Program.ApplicatonOwner.Id) await message.DeleteAsync(); }
                        catch { }
                        Log.Info($"[{context.Guild?.Name}/{context.Message.Channel?.Name}] {message.Author.Username} 執行 {context.Message}");
                    }
                }
            }
            else if (message.Channel is SocketTextChannel channel && message.MentionedUsers.Any((x) => x.Id == _client.CurrentUser.Id))
            {
                try
                {
                    var messageWithoutUser = message.Content.Replace("<@!", "<@").Replace($"<@{_client.CurrentUser.Id}>", "").Trim();
                    if (string.IsNullOrEmpty(messageWithoutUser))
                        return;                    

                    Log.Info($"[{channel.Guild?.Name}/{channel?.Name}] {message.Author.Username}: {messageWithoutUser}");
                    await _openAIService.HandleAIChat(channel!.Guild!.Id, channel, message.Author.Id, messageWithoutUser);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[{channel.Guild?.Name}/{channel?.Name}] {message.Author.Username} 發言出現錯誤");
                    await message.Channel.SendErrorAsync(ex.Message);
                }
            }
        }
    }
}
