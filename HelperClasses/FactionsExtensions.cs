using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace EEM.HelperClasses
{
    public static class FactionsExtensions
    {
        public static void DeclareWar(this IMyFaction OurFaction, IMyFaction HostileFaction, bool Print = false)
        {
            MyAPIGateway.Session.Factions.DeclareWar(OurFaction.FactionId, HostileFaction.FactionId);
            if (Print) AiSessionCore.DebugWrite($"{OurFaction.Tag}", $"Declared war on {HostileFaction.Tag}", antiSpam: false);
        }

        public static void ProposePeace(this IMyFaction OurFaction, IMyFaction HostileFaction, bool Print = false)
        {
            MyAPIGateway.Session.Factions.SendPeaceRequest(OurFaction.FactionId, HostileFaction.FactionId);
            if (Print) AiSessionCore.DebugWrite($"{OurFaction.Tag}", $"Proposed peace to {HostileFaction.Tag}", antiSpam: false);
        }

        public static void AcceptPeace(this IMyFaction OurFaction, IMyFaction HostileFaction, bool Print = false)
        {
            MyAPIGateway.Session.Factions.AcceptPeace(HostileFaction.FactionId, OurFaction.FactionId);
            MyAPIGateway.Session.Factions.AcceptPeace(OurFaction.FactionId, HostileFaction.FactionId);
            if (Print) AiSessionCore.DebugWrite($"{OurFaction.Tag}", $"Accepted peace from {HostileFaction.Tag}", antiSpam: false);
        }

        public static void DeclinePeace(this IMyFaction OurFaction, IMyFaction HostileFaction)
        {
            MyAPIGateway.Session.Factions.CancelPeaceRequest(OurFaction.FactionId, HostileFaction.FactionId);
        }

        public static bool IsHostileTo(this IMyFaction OurFaction, IMyFaction HostileFaction)
        {
            return MyAPIGateway.Session.Factions.AreFactionsEnemies(OurFaction.FactionId, HostileFaction.FactionId);
        }

        public static bool HasPeaceRequestTo(this IMyFaction OurFaction, IMyFaction HostileFaction)
        {
            return MyAPIGateway.Session.Factions.IsPeaceRequestStateSent(OurFaction.FactionId, HostileFaction.FactionId);
        }

        public static bool HasPeaceRequestFrom(this IMyFaction OurFaction, IMyFaction HostileFaction)
        {
            return MyAPIGateway.Session.Factions.IsPeaceRequestStatePending(OurFaction.FactionId, HostileFaction.FactionId);
        }

        public static bool IsPeacefulTo(this IMyFaction OurFaction, IMyFaction Faction, bool ConsiderPeaceRequests = false)
        {
            if (!ConsiderPeaceRequests)
                return MyAPIGateway.Session.Factions.GetRelationBetweenFactions
                           (OurFaction.FactionId, Faction.FactionId) != MyRelationsBetweenFactions.Enemies;
            else
                return OurFaction.IsPeacefulTo(Faction) || OurFaction.HasPeaceRequestTo(Faction);
        }

        public static bool IsLawful(this IMyFaction OwnFaction)
        {
            return Diplomacy.LawfulFactionsTags.Contains(OwnFaction.Tag);
        }

        public static void Accept(this IMyFaction Faction, IMyPlayer Player)
        {
            MyAPIGateway.Session.Factions.AcceptJoin(Faction.FactionId, Player.IdentityId);
        }

        public static void Kick(this IMyFaction Faction, IMyPlayer Member)
        {
            MyAPIGateway.Session.Factions.KickMember(Faction.FactionId, Member.IdentityId);
        }
    }
}