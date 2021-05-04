using Discord;
using Discord.Audio;
using Discord.WebSocket;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using YoutubeExtractor;
using YoutubeSearch;

namespace DiscordBot
{
    class Core
    {
        public static DiscordSocketClient Client;
        public static DiscordSocketConfig SocketConfig;
        public static Config BotConfig;
        public static CommandHandler handler;
        public static Dictionary<ulong, Guild> Guilds;
        public static Dictionary<Guild, AudioSession> AudioSessions;
        public static List<Command> Commands;
        public static bool DebugMode;
        public static List<ulong> DoNotLog = new List<ulong>();

        static void Main(string[] args) => new Core().StartBot(args).GetAwaiter().GetResult();

        public async Task StartBot(string[] args)
        {
            DebugMode = args.Contains("--debug");
            var Exit = false;
            Guilds = new Dictionary<ulong, Guild>();
            Commands = new List<Command>();
            AudioSessions = new Dictionary<Guild, AudioSession>();
            BotConfig = Config.GetConfig();

            SocketConfig = new DiscordSocketConfig();
            SocketConfig.LogLevel = LogSeverity.Verbose;
            SocketConfig.MessageCacheSize = 200;
            SocketConfig.AlwaysDownloadUsers = true;

            Client = new DiscordSocketClient(SocketConfig);

            Client.Log += LogService.Log;
            Client.GuildAvailable += async (g) =>
            {
                if (!Guilds.ContainsKey(g.Id))
                {
                    var x = await Guild.FromIdAsync(g.Id);
                    Guilds.Add(g.Id, x);
                    await LogService.Log(new LogMessage(LogSeverity.Verbose, "Guilds", $"Registered guild {g.Name}"));
                }
            };
            Client.JoinedGuild += async (g) =>
            {
                var x = await Guild.FromIdAsync(g.Id);
                Guilds.Add(x.Id, x);
                await LogService.Log(new LogMessage(LogSeverity.Verbose, "Guilds", $"Registered guild {g.Name}"));
            };
            Client.LeftGuild += async (g) =>
            {
                Guilds.Remove(g.Id);
                await Task.Delay(0);
                await LogService.Log(new LogMessage(LogSeverity.Verbose, "Guilds", $"Unregistered guild {g.Name}"));
            };
            Client.MessageReceived += async (e) =>
            {
                //await Refresh();
            };
            Client.MessageDeleted += async (m, ch) =>
            {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    try
                    {
                        var g = (ch as SocketGuildChannel).Guild;
                        var Guild = Guilds[g.Id];
                        if (!DoNotLog.Contains(m.Value.Id)) await Guild.LogAsync(new AuditLogEntry($"**Author**: {m.Value.Author.Mention}\n**Channel**: <#{m.Value.Channel.Id}>\n**Content**: " + m.Value.Content, "Message Deleted", new Color(255, 0, 0)));
                    }
                    catch { }
                }).Start();
                await Task.Delay(0);
            };
            Client.MessageUpdated += async (before, after, ch) =>
            {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    try
                    {
                        var g = (ch as SocketGuildChannel).Guild;
                        var Guild = Guilds[g.Id];
                        if (before.Value.Content != after.Content) await Guild.LogAsync(new AuditLogEntry($"**Author**: {after.Author.Mention}\n**Channel**: <#{after.Channel.Id}>\n**Old Message**: {before.Value.Content}\n**New Message**: {after.Content}", "Message Edited", new Color(50, 50, 200)));
                    }
                    catch { }
                }).Start();
                await Task.Delay(0);
            };
            Client.UserJoined += async (u) =>
            {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    try
                    {
                        var g = u.Guild;
                        var Guild = Guilds[g.Id];
                        await Guild.LogAsync(new AuditLogEntry($"**User**: {u.Username}#{u.Discriminator}\n**ID**: {u.Id.ToString()}", "User Joined"));
                    }
                    catch { }
                }).Start();
                await Task.Delay(0);
            };
            Client.UserLeft += async (u) =>
            {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    try
                    {
                        var g = u.Guild;
                        var Guild = Guilds[g.Id];
                        await Guild.LogAsync(new AuditLogEntry($"**User**: {u.Username}#{u.Discriminator}\n**ID**: {u.Id.ToString()}", "User Left"));
                    }
                    catch { }
                }).Start();
                await Task.Delay(0);
            };

            if (!DebugMode) await Client.LoginAsync(TokenType.Bot, BotConfig.Token);
            else await Client.LoginAsync(TokenType.Bot, BotConfig.DevToken);
            await Client.StartAsync();
            
            handler = new CommandHandler();
            await handler.Install(Client);

            Client.Disconnected += async (ex) =>
            {
                foreach (Guild g in Guilds.Values)
                {
                    await g.SaveToFolderAsync(Path.GetDirectoryName(Application.ExecutablePath) + "/Guilds/" + g.Id.ToString());
                }
                Exit = true;
            };

            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                while (!Exit)
                {
                    try
                    {
                        await Refresh();
                    }
                    catch { }
                    await Task.Delay(2000);
                }
            }).Start();

            
            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                while (!Exit)
                {
                    try
                    {
                        foreach (Guild g in Guilds.Values)
                        {
                            try
                            {
                                await g.SaveToFolderAsync(Path.GetDirectoryName(Application.ExecutablePath) + "/Guilds/" + g.Id.ToString());
                            }
                            catch { }
                        }
                    }
                    catch { }
                    await Task.Delay(60000);
                }
            }).Start();

            while (!Exit)
            {
                await Task.Delay(1);
            }
        }

        public static async Task Refresh()
        {
            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                try
                {
                    Thread.CurrentThread.IsBackground = true;

                    await Client.SetGameAsync((new Func<string>(() => { if (DebugMode && false) return "TEST MODE | "; else return ""; }))() + $"{BotConfig.BuildCode} | In {Client.Guilds.Count} Guilds", "https://twitch.tv/twitch", StreamType.Twitch);

                    // MUTE/BAN HANDLING
                    foreach (Guild Guild in Guilds.Values)
                    {
                        var g = Client.GetGuild(Guild.Id);

                        var RemoveWarns = new List<Warn>();
                        foreach (KeyValuePair<ulong, List<Warn>> x in Guild.Warns)
                        {
                            var u = g.GetUser(x.Key);
                            if (u == null) continue;
                            foreach (Warn w in x.Value)
                            {
                                if (!w.Expired)
                                {

                                }
                                else
                                {
                                    RemoveWarns.Add(w);
                                }
                            }
                        }
                        foreach (Warn w in RemoveWarns)
                        {
                            await Guild.RemoveWarnAsync(w);
                        }


                        var RemoveMutes = new List<Mute>();
                        foreach (KeyValuePair<ulong, List<Mute>> x in Guild.Mutes)
                        {
                            var u = g.GetUser(x.Key);
                            if (u == null) continue;
                            foreach (Mute m in x.Value)
                            {
                                if (!m.Expired)
                                {
                                    new Thread(async () =>
                                    {
                                        Thread.CurrentThread.IsBackground = true;

                                        foreach (SocketGuildChannel ch in g.Channels)
                                        {
                                            if (ch.GetPermissionOverwrite(u) == null)
                                            {
                                                /*var format = "";
                                                if (m.Ends < m.Starts + 60) format = @"ss\s";
                                                else if (m.Ends < m.Starts + (60 * 60)) format = @"mm\m ss\s";
                                                else if (m.Ends < m.Starts + (60 * 60 * 24)) format = @"hh\h mm\m ss\s";
                                                else if (m.Ends < m.Starts + (60 * 60 * 24 * 7)) format = @"dd\d hh\h mm\m ss\s";
                                                else if (m.Ends < m.Starts + (60 * 60 * 24 * 365)) format = @"ww\w dd\d hh\h mm\m ss\s";
                                                else format = @"yy\y ww\w dd\d hh\h mm\m ss\s";
                                                var length = new DateTime().AddSeconds(m.Ends - MiXLib.tick()).ToString(format);
                                                if (m.Permanent) length = "Forever";
                                                var issuer = g.GetUser(m.Issuer);
                                                await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed($"You have been muted in **{g.Name}** for **{m.Reason}** by {issuer.Username}#{issuer.Discriminator} ({issuer.Id.ToString()}) for **{length}**!"));*/
                                                try
                                                {
                                                    await ch.AddPermissionOverwriteAsync(u, new OverwritePermissions(0, 2099200));
                                                }
                                                catch (Exception ex)
                                                {
                                                    RemoveMutes.Add(m);
                                                }
                                            }
                                        }
                                    }).Start();
                                }
                                else
                                {
                                    new Thread(async () =>
                                    {
                                        Thread.CurrentThread.IsBackground = true;

                                        foreach (SocketGuildChannel ch in g.Channels)
                                        {
                                            if (ch.GetPermissionOverwrite(u) != null)
                                            {
                                                //await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed($"You have been unmuted in **{g.Name}**. Please follow the rules next time!"));
                                                try { await ch.RemovePermissionOverwriteAsync(u); } catch (Exception ex) { }
                                            }
                                        }
                                    }).Start();
                                    RemoveMutes.Add(m);
                                }
                            }
                        }
                        foreach (Mute m in RemoveMutes)
                        {
                            await Guild.RemoveMuteAsync(m);
                        }

                        var RemoveBans = new List<Ban>();
                        foreach (KeyValuePair<ulong, List<Ban>> x in Guild.Bans)
                        {
                            var u = g.GetUser(x.Key);
                            //if (u == null) continue;
                            foreach (Ban b in x.Value)
                            {
                                if (!b.Expired)
                                {
                                    new Thread(async () =>
                                    {
                                        Thread.CurrentThread.IsBackground = true;

                                        if (!(await g.GetBansAsync()).Any((ban) => { return ban.User.Id == x.Key; }))
                                        {
                                            /*var format = "";
                                            if (b.Ends < MiXLib.tick() + 60) format = @"ss\s";
                                            else if (b.Ends < MiXLib.tick() + (60 * 60)) format = @"mm\m ss\s";
                                            else if (b.Ends < MiXLib.tick() + (60 * 60 * 24)) format = @"hh\h mm\m ss\s";
                                            else if (b.Ends < MiXLib.tick() + (60 * 60 * 24 * 7)) format = @"dd\d hh\h mm\m ss\s";
                                            else if (b.Ends < MiXLib.tick() + (60 * 60 * 24 * 365)) format = @"ww\w dd\d hh\h mm\m ss\s";
                                            else format = @"yy\y ww\w dd\d hh\h mm\m ss\s";
                                            var length = new DateTime().AddSeconds(b.Ends - MiXLib.tick()).ToString(format);
                                            if (b.Permanent) length = "Forever";
                                            var issuer = g.GetUser(b.Issuer);
                                            await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed($"You have been banned in **{g.Name}** for **{b.Reason}** by {issuer.Username}#{issuer.Discriminator} ({issuer.Id.ToString()}) for **{length}**!"));*/
                                            try
                                            {
                                                await g.AddBanAsync(x.Key, 0, b.Reason);
                                            }
                                            catch (Exception ex)
                                            {
                                                RemoveBans.Add(b);
                                            }
                                        }
                                    }).Start();
                                }
                                else
                                {
                                    new Thread(async () =>
                                    {
                                        Thread.CurrentThread.IsBackground = true;

                                        if ((await g.GetBansAsync()).Any((ban) => { return ban.User.Id == x.Key; }))
                                        {
                                            //await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed($"You have been unbanned in **{g.Name}**. Please follow the rules next time!"));
                                            
                                        }
                                        RemoveBans.Add(b);
                                    }).Start();
                                }
                            }
                        }
                        foreach (Ban b in RemoveBans)
                        {
                            try { await Guild.RemoveBanAsync(b); } catch (Exception ex) { }
                            try { await g.RemoveBanAsync(b.User); } catch (Exception ex) { }
                        }
                    }
                }
                catch (Exception ex) { }
            }).Start();
        }

        public static Command GetCommand(string name)
        {
            foreach (Command c in Commands)
            {
                if (c.Name == name) return c;
            }
            return null;
        }
    }

    class LogService
    {
        public static async Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
        }

    }
}
