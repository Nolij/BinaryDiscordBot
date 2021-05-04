using System;
using System.Threading.Tasks;
using System.Reflection;
using DiscordBot;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System.Threading;

namespace DiscordBot
{
    public class CommandHandler
    {
        private CommandService commands;
        private DiscordSocketClient client;

        public async Task Install(DiscordSocketClient client)
        {
            this.client = client;
            commands = new CommandService();

            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
            await commands.AddModuleAsync(typeof(Modules.CommandModule));

            client.MessageReceived += HandleCommand;
        }

        public async Task HandleCommand(SocketMessage parameterMessage)
        {
            try
            {
                var g = (parameterMessage.Channel as SocketGuildChannel).Guild;
                var Guild = Core.Guilds[g.Id];

                var message = parameterMessage as SocketUserMessage;

                if (message == null) return;

                int argPos = 0;

                if (!message.HasStringPrefix(Guild.Config.Prefix, ref argPos)) return;

                var context = new SocketCommandContext(client, message);

                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    var result = await commands.ExecuteAsync(context, argPos);
                    try
                    {
                        if (!result.IsSuccess && !result.ErrorReason.ToLower().Contains("unknown command"))
                        {
                            //await message.AddReactionAsync(new Emoji("❌"));
                            await message.Channel.SendMessageAsync("", false, GetEmbed($"**Error:** {result.ErrorReason}", null, new Color(200, 0, 0)));
                            await LogService.Log(new LogMessage(LogSeverity.Error, "Commands", result.ErrorReason));
                        }
                    }
                    catch (Exception ex) { }
                }).Start();
            }
            catch (Exception ex) { }
            await Task.Delay(0);
        }

        public static Embed GetEmbed(string desc, string title = null, object color = null, string footer = null, string url = null, string authorname = null, string authoriconurl = null, string authorurl = null)
        {
            var embed = new EmbedBuilder()
                .WithDescription(desc);
            if (title != null)
            {
                embed.WithTitle(title);
            }
            if (color != null)
            {
                embed.WithColor((Color)color);
            }
            else
            {
                embed.WithColor(new Color(200, 0, 0));
            }
            if (footer != null)
            {
                embed.WithFooter(new EmbedFooterBuilder()
                    .WithText(footer));
            }
            if (url != null)
            {
                embed.WithUrl(url);
            }
            if (authorname != null)
            {
                var author = new EmbedAuthorBuilder()
                    .WithName(authorname);
                if (authoriconurl != null)
                {
                    author.WithIconUrl(authoriconurl);
                }
                if (authorurl != null)
                {
                    author.WithUrl(authorurl);
                }
                embed.WithAuthor(author);
            }
            return embed.Build();
        }
    }
}