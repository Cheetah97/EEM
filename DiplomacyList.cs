using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Cheetah.AI
{
    public static class Diplomacy
    {
        public static IMyFaction Police { get; private set; }
        public static IMyFaction Army { get; private set; }
        public static List<IMyFaction> LawfulFactions { get; private set; }
        //public static List<IMyFaction> NPCFactions { get; private set; }

        /// <summary>
        /// These factions are considered lawful. When they go hostile towards someone,
        /// they also make the police (SEPD) and army (UCMF) go hostile.
        /// </summary>
        public static readonly List<string> LawfulFactionsTags = new List<string>
        {
            "UCMF",
            "SEPD",
            "CIVL",
            "ISTG",
            "MA-I",
            "EXMC",
            "KUSS",
            "HS",
            "AMPH",
            "IMDC",
        };

        public static void Init()
        {
            Police = MyAPIGateway.Session.Factions.TryGetFactionByTag("SEPD");
            Army = MyAPIGateway.Session.Factions.TryGetFactionByTag("UCMF");
            LawfulFactions = MyAPIGateway.Session.Factions.Factions.Values.Where(x => LawfulFactionsTags.Contains(x.Tag)).ToList();
            //NPCFactions = MyAPIGateway.Session.Factions.Factions.Values.Where(x => x.IsNPC()).ToList();
        }
    }
}
