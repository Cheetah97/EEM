using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace EEM.HelperClasses
{
    public static class GeneralExtensions
    {
        public static bool IsNullEmptyOrWhiteSpace(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static bool IsValid(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntityInfo)
        {
            return !EntityInfo.IsEmpty();
        }

        public static bool IsHostile(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntityInfo)
        {
            return EntityInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies;
        }

        public static bool IsNonFriendly(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntityInfo)
        {
            return EntityInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies || EntityInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral;
        }

        public static IMyEntity GetEntity(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntityInfo)
        {
            return MyAPIGateway.Entities.GetEntityById(EntityInfo.EntityId);
        }

        /// <summary>
        /// Retrieves entity mass, in tonnes.
        /// </summary>
        public static float GetMassT(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntityInfo)
        {
            return EntityInfo.GetEntity().Physics.Mass / 1000;
        }

        public static IMyCubeGrid GetGrid(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntityInfo)
        {
            if (!EntityInfo.IsGrid()) return null;
            return MyAPIGateway.Entities.GetEntityById(EntityInfo.EntityId) as IMyCubeGrid;
        }

        public static bool IsGrid(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntityInfo)
        {
            return EntityInfo.Type == Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid || EntityInfo.Type == Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid;
        }

        public static void EnsureName(this IMyEntity Entity, string DesiredName = null)
        {
            if (Entity == null) return;
            if (DesiredName == null) DesiredName = $"Entity_{Entity.EntityId}";
            Entity.Name = DesiredName;
            MyAPIGateway.Entities.SetEntityName(Entity, false);
        }

        public static IMyFaction GetFaction(this IMyPlayer Player)
        {
            return MyAPIGateway.Session.Factions.TryGetPlayerFaction(Player.IdentityId);
        }

        public static bool IsMainCockpit(this IMyShipController ShipController)
        {
            return (ShipController as MyShipController).IsMainCockpit;
        }

        /// <summary>
        /// Returns block's builder id.
        /// </summary>
        public static long GetBuiltBy(this IMyCubeBlock Block)
        {
            return (Block as MyCubeBlock).BuiltBy;
        }

        /// <summary>
        /// Returns block's builder id. WARNING: Heavy!
        /// </summary>
        public static long GetBuiltBy(this IMySlimBlock Block)
        {
            if (Block is IMyCubeBlock)
                return (Block as MyCubeBlock).BuiltBy;
            MyObjectBuilder_CubeBlock builder = Block.GetObjectBuilder();
            return builder.BuiltBy;
        }

        public static bool IsNPC(this IMyFaction Faction)
        {
            try
            {
                IMyPlayer Owner = MyAPIGateway.Players.GetPlayerByID(Faction.FounderId);
                if (Owner != null) return Owner.IsBot;
                else
                {
                    if (Faction.Members.Count() == 0) return true;
                    foreach (KeyValuePair<long, MyFactionMember> member in Faction.Members)
                    {
                        IMyPlayer Member = MyAPIGateway.Players.GetPlayerByID(member.Value.PlayerId);
                        if (Member == null) continue;
                        if (!Member.IsBot) return false;
                    }
                    return true;
                }
            }
            catch (Exception Scrap)
            {
                AiSessionCore.LogError("Faction.IsNPC", Scrap);
                return false;
            }
        }

        public static bool IsPlayerFaction(this IMyFaction Faction)
        {
            return !Faction.IsNPC();
        }

        /*public static bool IsPeacefulNPC(this IMyFaction Faction)
        {
            try
            {
                if (!Faction.IsNPC()) return false;
                return Diplomacy.LawfulFactionsTags.Contains(Faction.Tag);
            }
            catch (Exception Scrap)
            {
                AISessionCore.LogError("Faction.IsPeacefulNPC", Scrap);
                return false;
            }
        }*/

        public static float GetHealth(this IMySlimBlock Block)
        {
            return Math.Min(Block.DamageRatio, Block.BuildLevelRatio);
        }

        public static IMyFaction FindOwnerFactionById(long IdentityID)
        {
            Dictionary<long, IMyFaction>.ValueCollection Factions = MyAPIGateway.Session.Factions.Factions.Values;
            foreach (IMyFaction Faction in Factions)
            {
                if (Faction.IsMember(IdentityID)) return Faction;
            }
            return null;
        }

        public static string Line(this string Str, int LineNumber, string NewlineStyle = "\r\n")
        {
            return Str.Split(NewlineStyle.ToCharArray())[LineNumber];
        }

        public static IMyPlayer GetPlayerByID(this IMyPlayerCollection Players, long PlayerID)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, x => x.IdentityId == PlayerID);
            return players.FirstOrDefault();
        }

        public static bool IsValidPlayer(this IMyPlayerCollection Players, long PlayerID, out IMyPlayer Player, bool CheckNonBot = true)
        {
            Player = MyAPIGateway.Players.GetPlayerByID(PlayerID);
            if (Player == null) return false;
            return CheckNonBot ? !Player.IsBot : true;
        }

        public static bool IsValidPlayer(this IMyPlayerCollection Players, long PlayerID, bool CheckNonBot = true)
        {
            IMyPlayer Player;
            return IsValidPlayer(Players, PlayerID, out Player);
        }
    }
}