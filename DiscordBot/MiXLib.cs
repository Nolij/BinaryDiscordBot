using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Net;
using System.IO;
using Discord.WebSocket;
using System.Diagnostics;
using Discord.Audio;

namespace DiscordBot
{
    public class MiXLib
    {
        public static async Task<Rank> GetRank(ulong id, SocketGuild guild)
        {
            var g = Core.Guilds[guild.Id];
            var u = guild.GetUser(id);
            if (id == Core.BotConfig.Owner) return Rank.BotOwner;
            else if (id == Core.Client.CurrentUser.Id) return Rank.BotOwner;
            else if (id == guild.OwnerId) return Rank.GuildOwner;
            else if (g.Users.ContainsKey(id)) return (Rank)g.Users[id];
            else if (u.Roles.Any((r) => { return r.Name.ToLower().Contains("admin"); })) return Rank.Administrator;
            else if (u.Roles.Any((r) => { return r.Name.ToLower().Contains("mod"); })) return Rank.Moderator;
            else return Rank.User;
        }

        public static string GetRankName(Rank rank) => GetRankName((int)rank);

        public static string GetRankName(int rank)
        {
            var rankname = "";
            switch (rank)
            {
                case 0:
                    rankname = "User";
                    break;
                case 1:
                    rankname = "Junior Mod";
                    break;
                case 2:
                    rankname = "Mod";
                    break;
                case 3:
                    rankname = "Senior Mod";
                    break;
                case 4:
                    rankname = "Admin";
                    break;
                case 5:
                    rankname = "Server Owner";
                    break;
                case 6:
                    rankname = "Bot Developer";
                    break;
                case 7:
                    rankname = "Bot Creator";
                    break;
                case -1:
                    rankname = "Muted";
                    break;
            }
            return rankname;
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
                embed.WithColor(new Color(0, 200, 0));
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

        public static string Concat(List<string> list, string sep = " ", string pre = "", string suf = "")
        {
            if (list.Count == 0)
            {
                return pre + "None" + suf;
            }
            var ret = "";
            foreach (string str in list)
            {
                ret = ret + sep + pre + str + suf;
            }
            ret = ret.Substring(sep.Length);
            return ret;
        }

        public static string BoolToYesNo(bool b)
        {
            switch (b)
            {
                case true:
                    return "Yes";
                case false:
                    return "No";
            }
            return null;
        }

        public static string BoolToEnabledDisabled(bool b)
        {
            switch (b)
            {
                case true:
                    return "ENABLED";
                case false:
                    return "DISABLED";
            }
            return null;
        }

        public static double tick()
        {
            return (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds / 1000;
        }

        public static async Task DownloadFileAsync(Uri uri, string filePath)
        {
            WebClient webClient = new WebClient();
            byte[] downloadedBytes = webClient.DownloadData(uri);
            while (downloadedBytes.Length == 0)
            {
                await Task.Delay(2000);
                downloadedBytes = webClient.DownloadData(uri);
            }
            Stream file = File.Open(filePath, FileMode.Create);
            file.Write(downloadedBytes, 0, downloadedBytes.Length);
            file.Close();
        }

        public static void CopyDirectory(string strSource, string strDestination)
        {
            if (!Directory.Exists(strDestination))
            {
                Directory.CreateDirectory(strDestination);
            }

            DirectoryInfo dirInfo = new DirectoryInfo(strSource);
            FileInfo[] files = dirInfo.GetFiles();
            foreach (FileInfo tempfile in files)
            {
                tempfile.CopyTo(Path.Combine(strDestination, tempfile.Name));
            }

            DirectoryInfo[] directories = dirInfo.GetDirectories();
            foreach (DirectoryInfo tempdir in directories)
            {
                CopyDirectory(Path.Combine(strSource, tempdir.Name), Path.Combine(strDestination, tempdir.Name));
            }

        }

        private static Process CreateStream(string path)
        {
            var ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i {path} -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            return Process.Start(ffmpeg);
        }

        public static async Task SendAudioAsync(IAudioClient client, string path)
        {
            var ffmpeg = CreateStream(path);
            var output = ffmpeg.StandardOutput.BaseStream;
            var discord = client.CreatePCMStream(AudioApplication.Mixed);
            await output.CopyToAsync(discord);
            await discord.FlushAsync();
        }

        public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp);
            return dtDateTime;
        }

        public static string FormatTime(int seconds)
        {
            /*var y = new DateTime().AddSeconds(seconds);
            if (seconds < 60) return $"{y.Second}s";
            else if (seconds < 60 * 60) return $"{y.Minute}m {y.Second}s";
            else if (seconds < (60 * 60) * 24) return $"{y.Hour}h {y.Minute}m {y.Second}s";
            else if (seconds < ((60 * 60) * 24) * 365) return $"{y.Day}d {y.Hour}h {y.Minute}m {y.Second}s";
            else return $"{y.Year}y {y.Day}d {y.Hour}h {y.Minute}m {y.Second}s";*/

            if (seconds < 1) return "Forever";

            var x = TimeSpan.FromSeconds(seconds);

            var Seconds = x.Seconds;
            var Minutes = x.Minutes;
            var Hours = x.Hours;
            var Days = ((Convert.ToDouble(x.Days) / 365) - (x.Days / 365)) * 365;
            var Years = x.Days / 365;

            var ret = "";

            if (Years != 0) ret = ret + $"{Years}y ";
            if (Days != 0) ret = ret + $"{Days}d ";
            if (Hours != 0) ret = ret + $"{Hours}h ";
            if (Minutes != 0) ret = ret + $"{Minutes}m ";
            if (Seconds != 0) ret = ret + $"{Seconds}s ";

            return ret.Substring(0, ret.Length - 1);
        }

        public static double StringToLength(string duration)
        {
            if (duration == null)
            {
                return 604800;
            }
            else if (duration == "inf" || duration == "perm" || duration == "permanent" || duration == "forever" || duration == "0" || duration == "-1")
            {
                return -1;
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
                    length = -1;
                }
                return length;
            }
        }
        
        public enum VideoType
        {
            nil = -1,
            Search = 0,
            URL = 1,
        }

        public enum Rank
        {
            Muted = -1,
            User = 0,
            JuniorModerator = 1,
            Moderator = 2,
            SeniorModerator = 3,
            Administrator = 4,
            GuildOwner = 5,
            BotDeveloper = 6,
            BotOwner = 7,
        }
    }
}
