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
using System.Threading.Tasks;
using System.Windows.Forms;
using YoutubeSearch;

namespace DiscordBot
{
    class Config
    {
        public string BuildCode { get; set; }
        public string Prefix { get; set; }
        public string Token { get; set; }
        public string DevToken { get; set; }
        public Dictionary<ulong, int> GlobalRanks { get; set; }
        public ulong Owner { get; set; }

        public static Config GetConfig()
        {
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText("Config.json"));
        }
    }

    class Guild
    {
        public Dictionary<ulong, int> Users { get; set; }
        public GuildConfig Config { get; set; }
        public Dictionary<ulong, List<Warn>> Warns { get; set; }
        public Dictionary<ulong, List<Mute>> Mutes { get; set; }
        public Dictionary<ulong, List<Ban>> Bans { get; set; }
        public ulong Id { get; set; }
        public bool InGuild { get { return Core.Client.GetGuild(Id) != null; } }

        public Guild(ulong id) => new Guild(id, new GuildConfig(), new Dictionary<ulong, int>(), new Dictionary<ulong, List<Warn>>(), new Dictionary<ulong, List<Mute>>(), new Dictionary<ulong, List<Ban>>());

        public Guild(ulong id, GuildConfig config, Dictionary<ulong, int> users, Dictionary<ulong, List<Warn>> warns, Dictionary<ulong, List<Mute>> mutes, Dictionary<ulong, List<Ban>> bans)
        {
            Id = id;
            Config = config;
            Users = users;
            Warns = warns;
            Mutes = mutes;
            Bans = bans;
        }

        public static async Task<Guild> FromFileAsync(string path)
        {
            throw new NotImplementedException();
        }

        public static async Task<Guild> FromFolderAsync(string path)
        {
            if (!Directory.Exists(path)) throw new FileNotFoundException();
            var id = Convert.ToUInt64(new DirectoryInfo(Path.GetDirectoryName(path + "/Config.json")).Name);
            var ret = new Guild(id);
            ret.Id = id;
            ret.Config = JsonConvert.DeserializeObject<GuildConfig>(File.ReadAllText(path + "/Config.json"));
            ret.Users = JsonConvert.DeserializeObject<Dictionary<ulong, int>>(File.ReadAllText(path + "/Users.json"));
            ret.Warns = new Dictionary<ulong, List<Warn>>();
            foreach (FileInfo f in new DirectoryInfo(path + "/Warns").GetFiles())
            {
                var uid = Convert.ToUInt64(f.Name.Substring(0, f.Name.Length - 5));
                try
                {
                    foreach (Warn w in JsonConvert.DeserializeObject<List<Warn>>(File.ReadAllText(f.FullName)))
                    {
                        await ret.AddWarnAsync(w, false);
                    }
                }
                catch
                {
                    f.Delete();
                }
            }
            ret.Mutes = new Dictionary<ulong, List<Mute>>();
            foreach (FileInfo f in new DirectoryInfo(path + "/Mutes").GetFiles())
            {
                var uid = Convert.ToUInt64(f.Name.Substring(0, f.Name.Length - 5));
                try
                {
                    foreach (Mute m in JsonConvert.DeserializeObject<List<Mute>>(File.ReadAllText(f.FullName)))
                    {
                        await ret.AddMuteAsync(m, false);
                    }
                }
                catch
                {
                    f.Delete();
                }
            }
            ret.Bans = new Dictionary<ulong, List<Ban>>();
            foreach (FileInfo f in new DirectoryInfo(path + "/Bans").GetFiles())
            {
                var uid = Convert.ToUInt64(f.Name.Substring(0, f.Name.Length - 5));
                try
                {
                    foreach (Ban b in JsonConvert.DeserializeObject<List<Ban>>(File.ReadAllText(f.FullName)))
                    {
                        await ret.AddBanAsync(b, false);
                    }
                }
                catch
                {
                    f.Delete();
                }
            }
            return ret;
        }

        public static async Task<Guild> FromIdAsync(ulong id)
        {
            if (!Directory.Exists(Path.GetDirectoryName(Application.ExecutablePath) + "/Guilds/" + id.ToString())) MiXLib.CopyDirectory(Path.GetDirectoryName(Application.ExecutablePath) + "/Guilds/0", Path.GetDirectoryName(Application.ExecutablePath) + "/Guilds/" + id.ToString());
            return await FromFolderAsync(Path.GetDirectoryName(Application.ExecutablePath) + "/Guilds/" + id.ToString());
        }

        public static async Task<Guild> FromDatabaseAsync(ulong id)
        {
            throw new NotImplementedException();
        }

        public async Task LogAsync(AuditLogEntry entry)
        {
            if (!Config.AuditLogsEnabled) return;
            var g = Core.Client.GetGuild(Id);
            var ch = g.GetChannel(Config.LogChannel) as SocketTextChannel;
            await ch.SendMessageAsync("", false, MiXLib.GetEmbed(entry.Message, entry.Action, entry.Color));
        }

        public async Task AddWarnAsync(Warn w, bool runtime = true)
        {
            var user = w.User;
            if (!Warns.ContainsKey(user)) Warns[user] = new List<Warn>();
            Warns[user].Add(w);
            if (runtime)
            {
                if (Warns[user].Count == 3) await AddMuteAsync(new Mute(w.User, Core.Client.CurrentUser.Id, "User has surpassed 3 warnings", MiXLib.tick(), MiXLib.tick() + 3600, false), false);
                else if (Warns[user].Count == 4) await KickUserAsync(w.User, Core.Client.CurrentUser.Id, "User has surpassed 4 warnings", false);
                else if (Warns[user].Count == 5) await AddBanAsync(new Ban(w.User, Core.Client.CurrentUser.Id, "User has surpassed 5 warnings", MiXLib.tick(), MiXLib.tick() + 604800, false), false);
                else if (Warns[user].Count >= 6) await AddBanAsync(new Ban(w.User, Core.Client.CurrentUser.Id, "User has surpassed 6 warnings", MiXLib.tick(), MiXLib.tick(), true), false);
                var g = Core.Client.GetGuild(Id);
                var u = g.GetUser(w.User);
                var issuer = g.GetUser(w.Issuer);
                var WarnCount = 1;
                if (Warns.ContainsKey(w.User)) WarnCount = Warns[w.User].Count;
                var Punishment = "None";
                if (WarnCount == 3) Punishment = "1h Mute";
                else if (WarnCount == 4) Punishment = "Kick";
                else if (WarnCount == 5) Punishment = "7d Tempban";
                else if (WarnCount >= 6) Punishment = "Permanent Ban";
                try
                {
                    await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed(
                    $"You have been warned in **{g.Name}**.\n\n" +
                    $"  Reason: **{w.Reason}**\n" +
                    $"  Issuer: **{issuer.Username}#{issuer.Discriminator}** ({issuer.Id.ToString()})\n" +
                    $"  Punishment: **{Punishment}**"));
                }
                catch (Exception ex) { }
                await LogAsync(new AuditLogEntry($"{issuer.Mention} warned {u.Mention} (**{WarnCount.ToString()}**/**6**): **{w.Reason}**", "General Moderation", new Color(255, 140, 0)));
            }
        }

        public async Task RemoveWarnAsync(Warn w, bool runtime = true)
        {
            var user = w.User;
            if (!Warns[user].Contains(w)) return;
            Warns[user].Remove(w);
        }

        public async Task AddMuteAsync(Mute m, bool runtime = true)
        {
            var user = m.User;
            if (!Mutes.ContainsKey(user)) Mutes[user] = new List<Mute>();
            Mutes[user].Add(m);
            if (runtime)
            {
                var g = Core.Client.GetGuild(Id);
                var u = g.GetUser(m.User);
                /*var format = "";
                if (m.Ends < m.Starts + 60) format = @"ss\s";
                else if (m.Ends < m.Starts + (60 * 60)) format = @"mm\m ss\s";
                else if (m.Ends < m.Starts + (60 * 60 * 24)) format = @"hh\h mm\m ss\s";
                else if (m.Ends < m.Starts + (60 * 60 * 24 * 7)) format = @"dd\d hh\h mm\m ss\s";
                else if (m.Ends < m.Starts + (60 * 60 * 24 * 365)) format = @"ww\w dd\d hh\h mm\m ss\s";
                else format = @"yy\y ww\w dd\d hh\h mm\m ss\s";
                var length = new DateTime().AddSeconds(m.Ends - MiXLib.tick()).ToString(format);*/
                var length = MiXLib.FormatTime(Convert.ToInt32(m.Ends - MiXLib.tick()));
                if (m.Permanent) length = "Forever";
                var issuer = g.GetUser(m.Issuer);
                try
                {
                    await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed(
                    $"You have been muted in **{g.Name}**.\n\n" +
                    $"  Reason: **{m.Reason}**\n" +
                    $"  Issuer: **{issuer.Username}#{issuer.Discriminator}** ({issuer.Id.ToString()})\n" +
                    $"  Duration: **{length}**"));
                }
                catch (Exception ex) { }
                await LogAsync(new AuditLogEntry($"{issuer.Mention} muted {u.Mention} for **{MiXLib.FormatTime(Convert.ToInt32(m.Ends - MiXLib.tick()))}**: **{m.Reason}**", "General Moderation", new Color(255, 0, 0)));
            }
        }

        public async Task RemoveMuteAsync(Mute m, bool runtime = true)
        {
            var user = m.User;
            if (!Mutes[user].Contains(m)) return;
            Mutes[user].Remove(m);
            if (runtime)
            {
                var g = Core.Client.GetGuild(Id);
                var u = g.GetUser(m.User);
                await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed($"You have been unmuted in **{g.Name}**. Please follow the rules next time!"));
                await LogAsync(new AuditLogEntry($"{u.Mention} was unmuted.", "General Moderation", new Color(255, 0, 0)));
            }
        }

        public async Task KickUserAsync(ulong user, ulong issuer, string reason, bool runtime = true)
        {
            try
            {
                if (runtime)
                {
                    try
                    {
                        var g = Core.Client.GetGuild(Id);
                        var u = g.GetUser(user);
                        var Issuer = g.GetUser(issuer);
                        try
                        {
                            await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed(
                            $"You have been kicked from **{g.Name}**.\n\n" +
                            $"  Reason: **{reason}**\n" +
                            $"  Issuer: **{Issuer.Username}#{Issuer.Discriminator}** ({Issuer.Id.ToString()})"));
                        }
                        catch (Exception ex) { }
                        await LogAsync(new AuditLogEntry($"{Issuer.Mention} kicked **{u.Username}#{u.Discriminator}**: **{reason}**", "General Moderation", new Color(0, 0, 0)));
                    }
                    catch { }
                }
                await Core.Client.GetGuild(Id).GetUser(user).KickAsync();
            }
            catch { }
        }

        public async Task AddBanAsync(Ban b, bool runtime = true)
        {
            var user = b.User;
            if (!Bans.ContainsKey(user)) Bans[user] = new List<Ban>();
            Bans[user].Add(b);
            if (runtime)
            {
                var g = Core.Client.GetGuild(Id);
                var u = g.GetUser(b.User);
                /*var format = "";
                if (b.Ends < MiXLib.tick() + 60) format = @"ss\s";
                else if (b.Ends < MiXLib.tick() + (60 * 60)) format = @"mm\m ss\s";
                else if (b.Ends < MiXLib.tick() + (60 * 60 * 24)) format = @"hh\h mm\m ss\s";
                else if (b.Ends < MiXLib.tick() + (60 * 60 * 24 * 7)) format = @"dd\d hh\h mm\m ss\s";
                else if (b.Ends < MiXLib.tick() + (60 * 60 * 24 * 365)) format = @"ww\w dd\d hh\h mm\m ss\s";
                else format = @"yy\y ww\w dd\d hh\h mm\m ss\s";
                var length = new DateTime().AddSeconds(b.Ends - MiXLib.tick()).ToString(format);*/
                var length = MiXLib.FormatTime(Convert.ToInt32(b.Ends - MiXLib.tick()));
                if (b.Permanent) length = "Forever";
                var issuer = g.GetUser(b.Issuer);
                var DateTimeFormat = @"MM\/dd\/yyyy \a\t hh:mm tt";
                try
                {
                    await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed(
                   $"You have been banned from **{g.Name}**.\n\n" +
                   $"  Reason: **{b.Reason}**\n" +
                   $"  Issuer: **{issuer.Username}#{issuer.Discriminator}** ({issuer.Id.ToString()})\n" +
                   $"  Duration: **{length}**\n\n" +
                   $"{(new Func<Task<string>>(async () => { if (b.Permanent) return ""; else return $"This ban expires on **{MiXLib.UnixTimeStampToDateTime(Convert.ToInt64(b.Ends)).ToString(DateTimeFormat)}**. You can rejoin the server at this time by using the invite link {(await g.DefaultChannel.CreateInviteAsync(0, 1, false, true)).Url}. Please note this link will only work for 1 person so do not use it on any other account or share it."; }))().GetAwaiter().GetResult()}"));
                }
                catch (Exception ex) { }
                await LogAsync(new AuditLogEntry($"{issuer.Mention} banned **{u.Username}#{u.Discriminator}** for **{MiXLib.FormatTime(Convert.ToInt32(b.Ends - MiXLib.tick()))}**: **{b.Reason}**", "General Moderation", new Color(1, 1, 1)));
            }
        }

        public async Task RemoveBanAsync(Ban b, bool runtime = true)
        {
            var user = b.User;
            if (!Bans[user].Contains(b)) return;
            Bans[user].Remove(b);
            if (runtime && false)
            {
                var g = Core.Client.GetGuild(Id);
                var u = g.GetUser(b.User);
                await (await u.GetOrCreateDMChannelAsync()).SendMessageAsync("", false, MiXLib.GetEmbed($"You have been unbanned from **{g.Name}**. Please follow the rules next time!"));
                await LogAsync(new AuditLogEntry($"**{u.Username}#{u.Discriminator}** was unbanned.", "General Moderation", new Color(1, 1, 1)));
            }
        }

        public async Task SaveToFolderAsync(string path)
        {
            File.WriteAllText(path + "/Config.json", JsonConvert.SerializeObject(Config));
            File.WriteAllText(path + "/Users.json", JsonConvert.SerializeObject(Users));
            foreach (KeyValuePair<ulong, List<Warn>> x in Warns)
            {
                var y = JsonConvert.SerializeObject(x.Value);
                if (y.Length == 0) y = "[]";
                File.WriteAllText(path + "/Warns/" + x.Key.ToString() + ".json", y);
            }
            foreach (KeyValuePair<ulong, List<Mute>> x in Mutes)
            {
                var y = JsonConvert.SerializeObject(x.Value);
                if (y.Length == 0) y = "[]";
                File.WriteAllText(path + "/Mutes/" + x.Key.ToString() + ".json", y);
            }
            foreach (KeyValuePair<ulong, List<Ban>> x in Bans)
            {
                var y = JsonConvert.SerializeObject(x.Value);
                if (y.Length == 0) y = "[]";
                File.WriteAllText(path + "/Bans/" + x.Key.ToString() + ".json", y);
            }

        }
    }

    class GuildConfig
    {
        public string Prefix { get; set; }
        public bool AuditLogsEnabled { get { return LogChannel != 0; } }
        public ulong LogChannel { get; set; }
        public bool EnableAntiSpam { get; set; }

        public GuildConfig() => new GuildConfig("!", 0, true);

        public GuildConfig(string prefix, ulong logchannel, bool antispam)
        {
            Prefix = prefix;
            LogChannel = logchannel;
            EnableAntiSpam = antispam;
        }
    }

    class AuditLogEntry
    {
        public string Message { get; set; }
        public string Action { get; set; }
        public Color Color { get; set; }

        public AuditLogEntry(string message, string action, object color = null)
        {
            Message = message;
            Action = action;
            Color = (Color)color;
        }
    }

    class AudioSession
    {
        public IAudioClient Client { get; set; }
        public Guild Guild { get; }
        public Dictionary<string, Song> Queue { get; set; }

        public AudioSession(IAudioClient client, Guild guild)
        {
            Client = client;
            Guild = guild;
            Queue = new Dictionary<string, Song>();
        }

        public async Task EndAsync()
        {
            Core.AudioSessions.Remove(Guild);
            Client = null;
            Client.StopAsync();
            foreach (Song s in Queue.Values)
            {
                s.Dispose();
            }
            Queue.Clear();
        }
    }

    class Song
    {
        public string Name { get; }
        public string URL { get; }
        public string ID { get; }
        public string FilePath { get; }
        public TimeSpan Duration { get; }

        public Song(string name, string url)
        {
            /*IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(url);
            VideoInfo video = videoInfos
                .Where(info => info.CanExtractAudio)
                .OrderByDescending(info => info.AudioBitrate)
                .FirstOrDefault();

            if (video == null) throw new Exception("No video found");
            
            if (video.RequiresDecryption)
            {
                DownloadUrlResolver.DecryptDownloadUrl(video);
            }

            //var audioDownloader = new AudioDownloader(video, Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) + "/AudioCache/", video.Title + video.AudioExtension));
            var audioDownloader = new AudioDownloader(video, Path.Combine("./AudioCache/", ID + video.AudioExtension));

            //audioDownloader.DownloadProgressChanged += (sender, args) => Console.WriteLine(args.ProgressPercentage * 0.85);
            //audioDownloader.AudioExtractionProgressChanged += (sender, args) => Console.WriteLine(85 + args.ProgressPercentage * 0.15);

            //FilePath = Path.GetDirectoryName(Application.ExecutablePath) + "/AudioCache/" + video.Title + video.AudioExtension;
            audioDownloader.Execute();*/

            Name = name;
            URL = url;
            ID = url.Substring(url.IndexOf("?v=") + 3);
            FilePath = "./AudioCache/" + ID + ".mp3";
            var DownloadURL = new WebClient().DownloadString("http://www.youtubeinmp3.com/fetch/?format=JSON&video=" + url);
            DownloadURL = DownloadURL.Substring(DownloadURL.IndexOf(",\"link\":") + 9, DownloadURL.Length - 2 - (DownloadURL.IndexOf(",\"link\":") + 9));
            MiXLib.DownloadFileAsync(new Uri(DownloadURL.Replace("\\", "")), Path.GetDirectoryName(Application.ExecutablePath) + $"/AudioCache/{ID}.mp3").GetAwaiter().GetResult();
            Mp3FileReader reader = new Mp3FileReader(FilePath);
            Duration = reader.TotalTime;
        }

        public static async Task<Song> FromSearchAsync(string query)
        {
            var x = new VideoSearch().SearchQuery(query, 1).FirstOrDefault();
            if (x == null) return null;
            return new Song(x.Title, x.Url);
        }

        public static async Task<Song> FromURLAsync(string url)
        {
            var id = url.Substring(url.IndexOf("?v=") + 3);
            var x = new WebClient().DownloadString($"https://www.googleapis.com/youtube/v3/videos?part=snippet&id={id}&key={"<REDACTED>"}");
            if (!x.StartsWith("{")) return null;
            var a = JsonConvert.DeserializeObject<Dictionary<string, object>>(x);
            var b = a.Values.LastOrDefault();
            var c = ((JArray)b).FirstOrDefault();
            var d = c.ToObject<Dictionary<string, object>>()["snippet"] as JObject;
            var title = d.ToObject<Dictionary<string, object>>()["title"] as string;
            return new Song(title, url);
        }

        public static async Task<string> IDFromSearchAsync(string query)
        {
            var x = new VideoSearch().SearchQuery(query, 1).FirstOrDefault();
            if (x == null) return null;
            return x.Url.Substring(x.Url.IndexOf("?v=") + 3);
        }

        public void Dispose()
        {
            File.Delete(FilePath);
        }
    }

    class Youtube
    {
    }

    class Warn
    {
        public ulong Issuer { get; }
        public ulong User { get; }
        public string Reason { get; }
        public double Starts { get; }
        public double Ends { get; }
        public bool Expired { get { return Ends != Starts && MiXLib.tick() > Ends; } }

        public Warn(ulong user, ulong issuer, string reason, double starts, double ends)
        {
            User = user;
            Issuer = issuer;
            Reason = reason;
            Starts = starts;
            Ends = ends;
        }
    }

    class Mute
    {
        public ulong Issuer { get; }
        public ulong User { get; }
        public string Reason { get; }
        public double Starts { get; }
        public double Ends { get; set; }
        public bool Permanent { get; set; }
        public bool Expired { get { return (Ends != Starts && MiXLib.tick() > Ends) && !Permanent; } set { if (value == true) { Ends = MiXLib.tick() - 1; Permanent = false; } } }

        public Mute(ulong user, ulong issuer, string reason, double starts, double ends, bool permanent)
        {
            User = user;
            Issuer = issuer;
            Reason = reason;
            Starts = starts;
            Ends = ends;
            Permanent = permanent;
        }
    }

    class Ban
    {
        public ulong Issuer { get; }
        public ulong User { get; }
        public string Reason { get; }
        public double Starts { get; }
        public double Ends { get; set; }
        public bool Permanent { get; set; }
        public bool Expired { get { return (Ends != Starts && MiXLib.tick() > Ends) && !Permanent; } set { if (value == true) { Ends = MiXLib.tick() - 1; Permanent = false; } } }

        public Ban(ulong user, ulong issuer, string reason, double starts, double ends, bool permanent)
        {
            User = user;
            Issuer = issuer;
            Reason = reason;
            Starts = starts;
            Ends = ends;
            Permanent = permanent;
        }
    }

    class Command
    {
        public string Name { get; }
        public string Description { get; }
        public string Usage { get; }
        public MiXLib.Rank Rank { get; }
        public bool Listed { get; }
        public List<string> Aliases { get; }
        public ulong Guild { get; }

        public Command(string name, string description, string usage, MiXLib.Rank rank = MiXLib.Rank.User, bool listed = true, ulong guild = 0, params string[] aliases)
        {
            Name = name;
            Description = description;
            Usage = usage;
            Rank = rank;
            Listed = listed;
            Aliases = new List<string>();
            Guild = guild;
            foreach (string s in aliases)
            {
                Aliases.Add(s);
            }
            LogService.Log(new LogMessage(LogSeverity.Debug, "Commands", "Registed Command " + Name)).GetAwaiter().GetResult();
        }
    }
}
