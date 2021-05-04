using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using DiscordBot;
using Discord;
using System.Collections.ObjectModel;

namespace DiscordBot.Attributes
{
    public class RegisterCommandAttribute : PreconditionAttribute
    {
        private string Name { get; set; }
        private string Description { get; set; }
        private string Usage { get; set; }
        private MiXLib.Rank Rank { get; set; }
        private bool Listed { get; set; }
        private List<string> Aliases { get; set; }
        private ulong Guild { get; set; }

        public RegisterCommandAttribute(string name, string description, string usage, MiXLib.Rank rank = MiXLib.Rank.User, bool listed = true, ulong guild = 0, params string[] aliases)
        {
            Name = name;
            Description = description;
            Usage = usage;
            Rank = rank;
            Listed = listed;
            Guild = guild;
            Aliases = new List<string>();
            foreach (string s in aliases)
            {
                Aliases.Add(s);
            }
            Core.Commands.Add(new Command(name, description, usage, rank, listed, guild, aliases));
        }

        public async override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var Context = context as SocketCommandContext;
            SocketGuildUser usr = Context.Guild.GetUser(context.User.Id);
            
            if (Guild != 0 && context.Guild.Id != Guild) return PreconditionResult.FromError("Unknown command.");
            else if (await MiXLib.GetRank(usr.Id, Context.Guild) < Rank) return PreconditionResult.FromError("You do not have permission to run this command!");
            return PreconditionResult.FromSuccess();
        }
    }
}
