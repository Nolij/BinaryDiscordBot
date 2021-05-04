using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DiscordBot.Attributes;
using Discord.WebSocket;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DiscordBot.Modules
{
    class CommandModule : ModuleBase<SocketCommandContext>
    {
        [Command("info")]
        [RegisterCommand("info", "Shows info", "info", 0, true)]
        public async Task Info()
        {
            var application = await Context.Client.GetApplicationInfoAsync();
            await ReplyAsync("", false, MiXLib.GetEmbed(
                    $"{Format.Bold("Info")}\n" +
                    $"- Creator: {application.Owner.Username}#{application.Owner.Discriminator} (ID {application.Owner.Id})\n" +
                    $"- Library: Discord.Net ({DiscordConfig.Version})\n" +
                    $"- Runtime: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.OSArchitecture}\n" +
                    $"- Uptime: {GetUptime()}\n\n" +

                    $"{Format.Bold("Stats")}\n" +
                    $"- Heap Size: {GetHeapSize()} MB\n" +
                    $"- Guilds: {(Context.Client as DiscordSocketClient).Guilds.Count}\n" +
                    $"- Channels: {(Context.Client as DiscordSocketClient).Guilds.Sum(g => g.Channels.Count)}\n" +
                    $"- Users: {(Context.Client as DiscordSocketClient).Guilds.Sum(g => g.MemberCount)}"
                ));
        }

        [Command("help"), Alias("?")]
        [RegisterCommand("help", "Displays list of commands and info about them", "help [ <command>]", 0, true, 0, "?")]
        public async Task HelpCommand([Remainder] string command = null)
        {
            var AvailableCommands = new List<Command>();
            foreach (Command c in Core.Commands)
            {
                if (c.Listed == false) continue;
                else if (c.Rank > await MiXLib.GetRank(Context.User.Id, Context.Guild)) continue;
                else if (c.Guild != 0 && c.Guild != Context.Guild.Id) continue;
                AvailableCommands.Add(c);
            }
            if (command == null)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed(
                    $"Rank: **{MiXLib.GetRankName(await MiXLib.GetRank(Context.User.Id, Context.Guild))}**\n" +
                    $"Available Commands (**{AvailableCommands.Count.ToString()}**): {(new Func<string>(() => { var cmds = new List<string>(); foreach (Command c in AvailableCommands) cmds.Add(c.Name); return MiXLib.Concat(cmds, ", ", "**", "**"); }))()}\n\n" +
                    $"Run **{Core.Guilds[Context.Guild.Id].Config.Prefix}help <command>** for more info"));
            }
            else
            {
                var Command = Core.GetCommand(command);
                if (Command == null || Command.Listed == false || (Command.Guild != 0 && Command.Guild != Context.Guild.Id))
                {
                    await ReplyAsync("", false, MiXLib.GetEmbed($"**Error**: Could not find command **{command}**.", null, new Color(200, 0, 0)));
                    return;
                }
                await ReplyAsync("", false, MiXLib.GetEmbed(
                    $"Command Name: **{Command.Name}**\n" +
                    $"Description: **{Command.Description}**\n" +
                    $"Aliases: {MiXLib.Concat(Command.Aliases, ", ", "**", "**")}\n" +
                    $"Usage: **{Command.Usage}**\n" +
                    $"Minimum Rank: **{MiXLib.GetRankName(Command.Rank)}**\n" +
                    $"User can run: **{MiXLib.BoolToYesNo(await MiXLib.GetRank(Context.User.Id, Context.Guild) >= Command.Rank)}**"
                    ));
            }
        }

        [Command("test123", RunMode = RunMode.Async)]
        [RegisterCommand("test123", "Used for testing purposes", "test123", MiXLib.Rank.BotOwner, true)]
        public async Task TestCommand(string time = null)
        {
            //var time = "";
            var reason = "";
            var IsPermanent = false;
            var EndTime = MiXLib.tick();
            if (time == null)
            {
                EndTime += 604800;
            }
            else if (time == "inf" || time == "perm" || time == "permanent" || time == "forever" || time == "0" || time == "-1")
            {
                IsPermanent = true;
                EndTime = -1;
            }
            else
            {
                double length = 0;
                try { length = Convert.ToDouble(time.Substring(0, time.Length - 1)); } catch (Exception ex) { length = Convert.ToDouble(time); }
                var suf = "";
                try { suf = time.Substring(time.Length - 1); } catch (Exception ex) { suf = "m"; }
                if (suf == "y")
                {
                    length = (((length * 60) * 60) * 24) * 365;
                }
                else if (suf == "d")
                {
                    length = ((length * 60) * 60) * 24;
                }
                else if (suf == "h")
                {
                    length = (length * 60) * 60;
                }
                else if (suf == "m")
                {
                    length = length * 60;
                }
                else if (suf == "s")
                {
                    length = length;
                }
                if (length <= 0)
                {
                    IsPermanent = true;
                    EndTime = -1;
                }
                else EndTime += length;
            }
            var LengthString = MiXLib.FormatTime(Convert.ToInt32(EndTime - MiXLib.tick()));
            if (IsPermanent)
            {
                LengthString = "**forever**";
            }
            if (reason == null)
            {
                reason = "";
            }
            await ReplyAsync("", false, MiXLib.GetEmbed(LengthString));
        }

        [Command("ping")]
        [RegisterCommand("ping", "Pong!", "ping")]
        public async Task Ping()
        {
            var t = MiXLib.tick();
            var msg = await ReplyAsync("", false, MiXLib.GetEmbed("Pinging..."));
            await msg.ModifyAsync(x =>
            {
                x.Embed = MiXLib.GetEmbed($"Pong! **{((int)((MiXLib.tick() - t) * 1000)).ToString()}ms**");
            });
        }

        [Command("logout", RunMode = RunMode.Async)]
        [RegisterCommand("logout", "Exits bot application", "logout", MiXLib.Rank.BotOwner, true)]
        public async Task Logout()
        {
            await ReplyAsync("", false, MiXLib.GetEmbed("Logging out..."));
            await Core.Client.SetStatusAsync(UserStatus.Invisible);
            foreach (AudioSession c in Core.AudioSessions.Values)
            {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    await c.EndAsync();
                }).Start();
            }
            await Task.Delay(100);
            await Core.Client.StopAsync();
        }

        [Command("setname")]
        [RegisterCommand("setname", "Set's username of the bot", "setname <name>", MiXLib.Rank.BotOwner)]
        public async Task SetName([Remainder] string name)
        {
            await Context.Client.CurrentUser.ModifyAsync(x =>
            {
                x.Username = name;
            });
            await ReplyAsync("", false, MiXLib.GetEmbed("Done!"));
        }

        [Command("avatar")]
        [RegisterCommand("avatar", "Sets bot avatar to attachment", "avatar", MiXLib.Rank.BotOwner, true)]
        public async Task Avatar()
        {
            var Image = Context.Message.Attachments.FirstOrDefault();
            if (Image == null)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** No image detected!", null, new Color(200, 0, 0)));
                return;
            }
            else if (!Image.Filename.EndsWith(".png"))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** Image is invalid! Please use a PNG file.", null, new Color(200, 0, 0)));
                return;
            }
            File.Delete("avatar.png");
            await MiXLib.DownloadFileAsync(new Uri(Image.Url), "avatar.png");
            await Context.Client.CurrentUser.ModifyAsync(x =>
            {
                x.Avatar = new Image(File.OpenRead("avatar.png"));
            });
            await ReplyAsync("", false, MiXLib.GetEmbed("Done!"));
        }

        [Command("purge", RunMode = RunMode.Async)]
        [RegisterCommand("purge", "Removes <num> messages in current channel (optional by <user>, only searches 1000 messages if user is specified)", "purge <num> [ <user>]", MiXLib.Rank.Moderator)]
        public async Task Purge(int num = 100, SocketUser user = null)
        {
            if (num > 100) num = 100;
            else if (num < 1) num = 1;
            if (user == null)
            {
                await Context.Message.DeleteAsync();
                var msgs = await Context.Channel.GetMessagesAsync(num).Flatten();
                foreach (IMessage msg in msgs) Core.DoNotLog.Add(msg.Id);
                await Context.Channel.DeleteMessagesAsync(msgs);
                var x = await ReplyAsync("", false, MiXLib.GetEmbed($"Deleted **{msgs.Count()}** messages."));
                Core.DoNotLog.Add(x.Id);
                await Task.Delay(2000);
                await x.DeleteAsync();
            }
            else
            {
                var msgs = new List<IMessage>();
                var i = 0;
                var lastmessage = Context.Message.Id;
                while (msgs.Count < num && i < 10)
                {
                    var ms = await Context.Channel.GetMessagesAsync(lastmessage, Direction.Before).Flatten();
                    foreach (IMessage m in ms)
                    {
                        if (m.Author.Id == user.Id)
                        {
                            msgs.Add(m);
                            if (msgs.Count >= num) break;
                        }
                    }
                    if (ms.LastOrDefault() != null) lastmessage = ms.LastOrDefault().Id;
                    else break;
                    i = i + 1;
                }
                foreach (IMessage msg in msgs) Core.DoNotLog.Add(msg.Id);
                await Context.Channel.DeleteMessagesAsync(msgs);
                var x = await ReplyAsync("", false, MiXLib.GetEmbed($"Deleted **{msgs.Count()}** messages."));
                Core.DoNotLog.Add(x.Id);
                await Task.Delay(2000);
                await x.DeleteAsync();
            }
        }

        [Command("subscribe")]
        [RegisterCommand("subscribe", "Subscribes to bot update notifications", "subscribe", MiXLib.Rank.User, true, 247765267424215040)]
        public async Task Subscribe()
        {
            ulong RoleId = 332951002867302400;
            if (Context.Guild.GetUser(Context.User.Id).Roles.Contains(Context.Guild.GetRole(RoleId)))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User already has role!", null, new Color(200, 0, 0)));
                return;
            }
            await Context.Guild.GetUser(Context.User.Id).AddRoleAsync(Context.Guild.GetRole(RoleId));
            await ReplyAsync("", false, MiXLib.GetEmbed($"You will now get notified whenever there is a progress update or new feature in the bot. You can unsubscribe by typing **{Core.BotConfig.Prefix}unsubscribe**!"));
        }

        [Command("unsubscribe")]
        [RegisterCommand("unsubscribe", "Unsubscribes from bot update notifications", "unsubscribe", MiXLib.Rank.User, true, 247765267424215040)]
        public async Task Unsubscribe()
        {
            ulong RoleId = 332951002867302400;
            if (!Context.Guild.GetUser(Context.User.Id).Roles.Contains(Context.Guild.GetRole(RoleId)))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User does not have role!", null, new Color(200, 0, 0)));
                return;
            }
            await Context.Guild.GetUser(Context.User.Id).RemoveRoleAsync(Context.Guild.GetRole(RoleId));
            await ReplyAsync("", false, MiXLib.GetEmbed($"You will no longer get notified whenever there is a progress update or new feature in the bot. You can subscribe again by typing **{Core.BotConfig.Prefix}subscribe**!"));
        }

        [Command("warn"), Alias("w")]
        [RegisterCommand("warn", "Gives user <user> a warning (optional reason <reason>)", "warn <user> [ <reason>]", MiXLib.Rank.JuniorModerator, true, 0, "w")]
        public async Task Warn(SocketUser user, [Remainder] string reason = "Unspecified Reason")
        {
            if (await MiXLib.GetRank(user.Id, Context.Guild) > 0)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User is a staff member, cannot warn.", color: new Color(200, 0, 0)));
                return;
            }
            var w = new Warn(user.Id, Context.User.Id, reason, MiXLib.tick(), MiXLib.tick() + 18000);
            await Core.Guilds[Context.Guild.Id].AddWarnAsync(w);
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully warned user.", color: new Color(255, 140, 0)));
        }

        [Command("warns"), Alias("getwarns")]
        [RegisterCommand("warns", "Gets list of all active warnings for <user>", "warns <user>", MiXLib.Rank.JuniorModerator, aliases: "getwarns")]
        public async Task GetWarns(SocketUser user)
        {
            var Guild = Core.Guilds[Context.Guild.Id];
            if (!Guild.Warns.ContainsKey(user.Id) || Guild.Warns[user.Id].Count <= 0)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("User has no active warnings!", color: new Color(255, 140, 0)));
                return;
            }
            var Warns = Guild.Warns[user.Id];
            var str = "";
            foreach (Warn w in Warns)
            {
                var DateTimeFormat = @"MM\/dd\/yyyy  hh:mm tt";
                var Issuer = Context.Guild.GetUser(w.Issuer);
                str = str +
                    $"{MiXLib.UnixTimeStampToDateTime(Convert.ToInt64(w.Starts)).ToString(DateTimeFormat)}\n" +
                    $"  Reason: **{w.Reason}**\n" +
                    $"  Issuer: **{Issuer.Username}#{Issuer.Discriminator}** ({Issuer.Id})\n" +
                    $"  Expired: **{MiXLib.BoolToYesNo(w.Expired)}**\n" +
                    $"  Expires: **{MiXLib.UnixTimeStampToDateTime(Convert.ToInt64(w.Ends)).ToString(DateTimeFormat)}** (in **{MiXLib.FormatTime(Convert.ToInt32(w.Ends - MiXLib.tick()))}**)\n\n";
            }
            await ReplyAsync("", false, MiXLib.GetEmbed(str, $"Warnings for {user.Username}#{user.Discriminator}", new Color(255, 140, 0)));
        }

        [Command("delwarn")]
        [RegisterCommand("delwarn", "Deletes latest warning for <user>", "delwarn <user>", MiXLib.Rank.Moderator)]
        public async Task DeleteWarn(SocketUser user)
        {
            var Guild = Core.Guilds[Context.Guild.Id];
            if (!Guild.Warns.ContainsKey(user.Id) || Guild.Warns[user.Id].Count <= 0)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("User has no active warnings!", color: new Color(255, 140, 0)));
                return;
            }
            var Warns = Guild.Warns[user.Id];
            await Guild.RemoveWarnAsync(Warns.LastOrDefault());
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully removed warning.", color: new Color(255, 140, 0)));
        }

        [Command("clearwarns"), Alias("cwarns")]
        [RegisterCommand("clearwarns", "Deletes all warnings for <user>", "clearwarns <user>", MiXLib.Rank.Moderator, aliases: "cwarns")]
        public async Task ClearWarns(SocketUser user)
        {
            var Guild = Core.Guilds[Context.Guild.Id];
            if (!Guild.Warns.ContainsKey(user.Id) || Guild.Warns[user.Id].Count <= 0)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("User has no active warnings!", color: new Color(255, 140, 0)));
                return;
            }
            var Warns = Guild.Warns[user.Id];
            var RemoveWarns = new List<Warn>();
            foreach (Warn w in Warns) RemoveWarns.Add(w);
            foreach (Warn w in RemoveWarns) await Guild.RemoveWarnAsync(w);
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully cleared warnings.", color: new Color(255, 140, 0)));
        }

        [Command("mute"), Alias("m")]
        [RegisterCommand("mute", "Prevents <user> from talking for <duration>", "warn <user> <duration> [ <reason>]", MiXLib.Rank.Moderator, true, 0, "m")]
        public async Task Mute(SocketUser user, string duration = null, [Remainder] string reason = "Unspecified Reason")
        {
            if (await MiXLib.GetRank(user.Id, Context.Guild) >= await MiXLib.GetRank(Context.User.Id, Context.Guild))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** You do not outrank this user.", color: new Color(200, 0, 0)));
                return;
            }
            var IsPermanent = false;
            var EndTime = MiXLib.tick();
            if (duration == null)
            {
                EndTime += 604800;
            }
            else if (duration == "inf" || duration == "perm" || duration == "permanent" || duration == "forever" || duration == "0" || duration == "-1")
            {
                IsPermanent = true;
                EndTime = -1;
            }
            else
            {
                double length = 0;
                try { length = Convert.ToDouble(duration.Substring(0, duration.Length - 1)); } catch (Exception ex) { length = Convert.ToDouble(duration); }
                var suf = "";
                try { suf = duration.Substring(duration.Length - 1); } catch (Exception ex) { suf = "m"; }
                try { Convert.ToDouble(suf); length = Convert.ToDouble(duration); suf = "m"; } catch (Exception ex) { }
                if (suf == "y")
                {
                    length = (((length * 60) * 60) * 24) * 365;
                }
                else if (suf == "d")
                {
                    length = ((length * 60) * 60) * 24;
                }
                else if (suf == "h")
                {
                    length = (length * 60) * 60;
                }
                else if (suf == "m")
                {
                    length = length * 60;
                }
                else if (suf == "s")
                {
                    length = length;
                }
                if (length <= 0)
                {
                    IsPermanent = true;
                    EndTime = -1;
                }
                else EndTime += length;
            }
            /*var LengthString = MiXLib.FormatTime(Convert.ToInt32(EndTime - MiXLib.tick()));
            if (IsPermanent)
            {
                LengthString = "forever";
            }*/
            var m = new Mute(user.Id, Context.User.Id, reason, MiXLib.tick(), EndTime, IsPermanent);
            await Core.Guilds[Context.Guild.Id].AddMuteAsync(m);
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully muted user.", color: new Color(255, 0, 0)));
        }

        [Command("unmute"), Alias("um")]
        [RegisterCommand("unmute", "Unmutes <user>", "unmute <user>", MiXLib.Rank.JuniorModerator, aliases: "um")]
        public async Task Unmute(SocketUser user)
        {
            var Guild = Core.Guilds[Context.Guild.Id];
            if (!Guild.Mutes.ContainsKey(user.Id) || Guild.Mutes[user.Id].Count <= 0)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User is not muted!", color: new Color(200, 0, 0)));
                return;
            }
            var Mutes = Guild.Mutes[user.Id];
            var Remove = new List<Mute>();
            foreach (Mute m in Mutes) Remove.Add(m);
            foreach (Mute m in Remove) m.Expired = true;
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully unmuted user.", color: new Color(255, 0, 0)));
        }

        [Command("kick"), Alias("k")]
        [RegisterCommand("kick", "Kicks user from guild", "kick <user> [ <reason>]", MiXLib.Rank.SeniorModerator, aliases: "k")]
        public async Task Kick(SocketUser user, [Remainder] string reason = "Unspecified Reason")
        {
            if (await MiXLib.GetRank(user.Id, Context.Guild) >= await MiXLib.GetRank(Context.User.Id, Context.Guild))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** You do not outrank this user.", color: new Color(200, 0, 0)));
                return;
            }
            else if (Context.Guild.GetUser(user.Id).Hierarchy > Context.Guild.GetUser(Context.Client.CurrentUser.Id).Hierarchy || Context.Guild.OwnerId == user.Id)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User outranks bot, no permissions.", color: new Color(200, 0, 0)));
                return;
            }
            await Core.Guilds[Context.Guild.Id].KickUserAsync(user.Id, Context.User.Id, reason);
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully kicked user.", color: new Color(0, 0, 0)));
        }

        [Command("ban"), Alias("b")]
        [RegisterCommand("ban", "Bans user from guild for <duration>", "ban <user> <duration> [ <reason>]", MiXLib.Rank.Administrator, true, 0, "b")]
        public async Task Ban(SocketUser user, string duration = "-1", [Remainder] string reason = "Unspecified Reason")
        {
            if (await MiXLib.GetRank(user.Id, Context.Guild) >= await MiXLib.GetRank(Context.User.Id, Context.Guild))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** You do not outrank this user.", color: new Color(200, 0, 0)));
                return;
            }
            else if (Context.Guild.GetUser(user.Id).Hierarchy > Context.Guild.GetUser(Context.Client.CurrentUser.Id).Hierarchy || Context.Guild.OwnerId == user.Id)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User outranks bot, no permissions.", color: new Color(200, 0, 0)));
                return;
            }
            var IsPermanent = false;
            var EndTime = MiXLib.tick();
            if (duration == null)
            {
                EndTime += 604800;
            }
            else if (duration == "inf" || duration == "perm" || duration == "permanent" || duration == "forever" || duration == "0" || duration == "-1")
            {
                IsPermanent = true;
                EndTime = -1;
            }
            else
            {
                double length = 0;
                try { length = Convert.ToDouble(duration.Substring(0, duration.Length - 1)); } catch (Exception ex) { length = Convert.ToDouble(duration); }
                var suf = "";
                try { suf = duration.Substring(duration.Length - 1); } catch (Exception ex) { suf = "m"; }
                try { Convert.ToDouble(suf); length = Convert.ToDouble(duration); suf = "m"; } catch (Exception ex) { }
                if (suf == "y")
                {
                    length = (((length * 60) * 60) * 24) * 365;
                }
                else if (suf == "d")
                {
                    length = ((length * 60) * 60) * 24;
                }
                else if (suf == "h")
                {
                    length = (length * 60) * 60;
                }
                else if (suf == "m")
                {
                    length = length * 60;
                }
                else if (suf == "s")
                {
                    length = length;
                }
                if (length <= 0)
                {
                    IsPermanent = true;
                    EndTime = -1;
                }
                else EndTime += length;
            }
            /*var LengthString = MiXLib.FormatTime(Convert.ToInt32(EndTime - MiXLib.tick()));
            if (IsPermanent)
            {
                LengthString = "forever";
            }*/
            var b = new Ban(user.Id, Context.User.Id, reason, MiXLib.tick(), EndTime, IsPermanent);
            await Core.Guilds[Context.Guild.Id].AddBanAsync(b);
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully banned user.", color: new Color(1, 1, 1)));
        }

        [Command("unban")]
        [RegisterCommand("unban", "Unbans user with id <userid>", "unmute <userid>", MiXLib.Rank.SeniorModerator)]
        public async Task Unban(ulong user)
        {
            var Guild = Core.Guilds[Context.Guild.Id];
            if (!Guild.Bans.ContainsKey(user) || Guild.Bans[user].Count <= 0)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User is not banned!", color: new Color(200, 0, 0)));
                return;
            }
            var Mutes = Guild.Bans[user];
            var Remove = new List<Ban>();
            foreach (Ban b in Mutes) Remove.Add(b);
            foreach (Ban b in Remove) b.Expired = true;
            await ReplyAsync("", false, MiXLib.GetEmbed("Successfully unbanned user.", color: new Color(1, 1, 1)));
        }

        [Command("play", RunMode = RunMode.Async)]
        [RegisterCommand("play", "Plays audio from youtube video url/search <video> in channel that running user is in", "play <video>", MiXLib.Rank.BotDeveloper, true)]
        public async Task Play([Remainder] string video)
        {
            var New = false;
            var Guild = Core.Guilds[Context.Guild.Id];
            var User = Context.Guild.GetUser(Context.User.Id);
            if (User.VoiceChannel == null && !(Core.AudioSessions.ContainsKey(Guild) && await MiXLib.GetRank(User.Id, Context.Guild) > MiXLib.Rank.User))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User is not in a voice channel.", null, new Color(200, 0, 0)));
                return;
            }
            AudioSession AudioSession = null;
            if (!Core.AudioSessions.ContainsKey(Guild))
            {
                var client = await User.VoiceChannel.ConnectAsync();
                AudioSession = new AudioSession(client, Guild);
                Core.AudioSessions[Guild] = AudioSession;
                New = true;
            }
            else if (User.VoiceChannel != Context.Guild.GetUser(Context.Client.CurrentUser.Id).VoiceChannel && await MiXLib.GetRank(User.Id, Context.Guild) < MiXLib.Rank.JuniorModerator)
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** User is in different voice channel.", null, new Color(200, 0, 0)));
                return;
            }
            else AudioSession = Core.AudioSessions[Guild];
            MiXLib.VideoType type = MiXLib.VideoType.nil;
            if (
                (
                video.StartsWith("https://www.youtube.com/watch?v=") ||
                video.StartsWith("http://www.youtube.com/watch?v=") ||
                video.StartsWith("https://youtube.com/watch?v=") ||
                video.StartsWith("http://youtube.com/watch?v=") /*||
                video.StartsWith("https://youtu.be/") ||
                video.StartsWith("http://youtu.be/") */
                ) &&
                !video.Contains(' ') &&
                !video.Contains('\n')
                )
                type = MiXLib.VideoType.URL;
            else type = MiXLib.VideoType.Search;
            var id = "";
            if (type == MiXLib.VideoType.Search) id = await Song.IDFromSearchAsync(video);
            else id = video.Substring(video.IndexOf("?v=") + 3);
            if (AudioSession.Queue.ContainsKey(id))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** Cannot add song that is already queued into the queue.", null, new Color(200, 0, 0)));
                return;
            }
            Song song;
            if (type == MiXLib.VideoType.URL) song = await Song.FromURLAsync(video);
            else song = await Song.FromSearchAsync(video);
            AudioSession.Queue.Add(song.ID, song);
            if (New) while (AudioSession.Queue.Count > 0)
                {
                    AudioSession.Client.Disconnected += async (ex) =>
                    {
                        await Task.Delay(0);
                        AudioSession.Queue.Clear();
                        Core.AudioSessions.Remove(Guild);
                        if (ex == null) await ReplyAsync("", false, MiXLib.GetEmbed("Queue Concluded."));
                    };
                    var Song = AudioSession.Queue.FirstOrDefault().Value;
                    await ReplyAsync("", false, MiXLib.GetEmbed($"Playing **{Song.Name}**"));
                    await MiXLib.SendAudioAsync(AudioSession.Client, Song.FilePath);
                    await Task.Delay(Song.Duration);
                    Song.Dispose();
                    AudioSession.Queue.Remove(Song.ID);
                    await Task.Delay(1000);
                }
            await AudioSession.EndAsync();
            await ReplyAsync("", false, MiXLib.GetEmbed("Queue Concluded."));
        }

        [Command("stop")]
        [RegisterCommand("stop", "Stops current playing song.", "stop", MiXLib.Rank.BotDeveloper)]
        public async Task Stop()
        {
            if (!Core.AudioSessions.ContainsKey(Core.Guilds[Context.Guild.Id]))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** No song is playing.", null, new Color(200, 0, 0)));
                return;
            }
            await Core.AudioSessions[Core.Guilds[Context.Guild.Id]].EndAsync();
            await ReplyAsync("", false, MiXLib.GetEmbed("Done."));
        }

        [Command("prefix")]
        [RegisterCommand("prefix", "Sets guild's prefix to <prefix> (or **!** if no prefix is provided)", "prefix <prefix>", MiXLib.Rank.GuildOwner)]
        public async Task Prefix([Remainder] string prefix = "!")
        {
            Core.Guilds[Context.Guild.Id].Config.Prefix = prefix;
            await ReplyAsync("", false, MiXLib.GetEmbed($"Successfully set this guild's prefix to **{prefix}**"));
        }

        [Command("logchannel")]
        [RegisterCommand("logchannel", "Sets channel for audit logs, please make this channel only visible to staff", "logchannel", MiXLib.Rank.GuildOwner)]
        public async Task LogChannel()
        {
            Core.Guilds[Context.Guild.Id].Config.LogChannel = Context.Channel.Id;
        }

        [Command("setrank")]
        [RegisterCommand("setrank", "Sets <user>'s rank to <rank>", "setrank <user> <rank>", MiXLib.Rank.GuildOwner)]
        public async Task SetRank(SocketUser user, [Remainder] string rankstr)
        {
            var rank = MiXLib.Rank.User;
            try
            {
                rank = (MiXLib.Rank)Convert.ToInt32(rankstr);
            }
            catch (Exception ex)
            {
                rankstr = rankstr.ToLower();
                if (rankstr.Contains("owner")) rank = MiXLib.Rank.GuildOwner;
                else if (rankstr.Contains("admin")) rank = MiXLib.Rank.Administrator;
                else if ((rankstr.Contains("sr") || rankstr.Contains("senior")) && rankstr.Contains("mod")) rank = MiXLib.Rank.SeniorModerator;
                else if (!(rankstr.Contains("sr") || rankstr.Contains("senior")) && !(rankstr.Contains("jr") || rankstr.Contains("junior")) && rankstr.Contains("mod")) rank = MiXLib.Rank.Moderator;
                else if ((rankstr.Contains("jr") || rankstr.Contains("junior")) && rankstr.Contains("mod")) rank = MiXLib.Rank.JuniorModerator;
                else rank = MiXLib.Rank.User;
            };
            if (rank > await MiXLib.GetRank(Context.User.Id, Context.Guild))
            {
                await ReplyAsync("", false, MiXLib.GetEmbed("**Error:** You cannot set peoples ranks to a rank higher than your own.", null, new Color(200, 0, 0)));
                return;
            }
            Core.Guilds[Context.Guild.Id].Users[user.Id] = (int)rank;
            await ReplyAsync("", false, MiXLib.GetEmbed($"Successfully set {user.Mention}'s rank to **{MiXLib.GetRankName(rank)}**!"));
        }

        [Command("embed")]
        [RegisterCommand("embed", "Sends embed with <text> as its body", "embed <text>", MiXLib.Rank.BotOwner)]
        public async Task Embed([Remainder] string text = "")
        {
            await ReplyAsync("", false, MiXLib.GetEmbed(text));
        }

        [Command("poll")]
        [RegisterCommand("poll", "Starts a poll", "poll <duration> <option1>; <option2>; <option3> etc...", MiXLib.Rank.BotDeveloper)]
        public async Task Poll(string duration, [Remainder] string options)
        {

        }


        private static string GetUptime()
            => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\:hh\:mm\:ss");
        private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString();
    }
}
