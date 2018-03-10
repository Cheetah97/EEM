using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using SpaceEngineers.Game.Weapons.Guns;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using static EEM.Constants;
using static EEM.HelperClasses.StaticTools;

namespace EEM.HelperClasses
{
    public static class DamageHelper
    {
        private static readonly LogWriter DebugLog = new LogWriter(LogNameDamageHelper);


/*
        /// <summary>
        /// Determines if damage was done by player.
        /// <para/>
        /// If it's necessary to determine who did the damage, use overload.
        /// </summary>
        public static bool IsDoneByPlayer(this MyDamageInformation damage)
        {
            IMyPlayer trash;
            return damage.IsDoneByPlayer(out trash);
        }
*/

        static bool IsDamagedByPlayerWarhead(IMyWarhead warhead, out IMyPlayer damager)
        {
            damager = null;
            try
            {
                if (warhead.OwnerId == 0)
                {
                    damager = MyAPIGateway.Players.GetPlayerByID(((MyCubeBlock) warhead).BuiltBy);
                    AiSessionCore.DebugWrite("Damage.IsDoneByPlayer", "Attempting to find damager by neutral warhead.");
                    return damager != null;
                }
                else
                {
                    damager = MyAPIGateway.Players.GetPlayerByID(warhead.OwnerId);
                    AiSessionCore.DebugWrite("Damage.IsDoneByPlayer", "Attempting to find damager by warhead owner.");
                    return damager != null;
                }
            }
            catch (Exception scrap)
            {
                AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check for neutral warheads crashed", scrap));
                return false;
            }
        }

        static bool IsDamagedByPlayer(IMyGunBaseUser gun, out IMyPlayer damager)
        {
            damager = null;
            try
            {
                damager = MyAPIGateway.Players.GetPlayerByID(gun.OwnerId);
                //AISessionCore.DebugWrite($"GunDamage.IsDamagedByPlayer", $"Getting player from gun. ID: {Gun.OwnerId}, player: {(Damager != null ? Damager.DisplayName : "null")}", false);
                return damager != null && !damager.IsBot;
            }
            catch (Exception scrap)
            {
                AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check gun owner crashed", scrap));
                return false;
            }
        }
        static bool IsDamagedByPlayer(IMyEngineerToolBase tool, out IMyPlayer damager)
        {
            damager = null;
            try
            {
                damager = MyAPIGateway.Players.GetPlayerByID(tool.OwnerIdentityId);
                //AISessionCore.DebugWrite($"ToolDamage.IsDamagedByPlayer", $"Getting player from tool. ID: {Tool.OwnerId}, IdentityID: {Tool.OwnerIdentityId}, player: {(Damager != null ? Damager.DisplayName : "null")}", false);
                return !damager?.IsBot ?? false;
            }
            catch (Exception scrap)
            {
                AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check gun owner crashed", scrap));
                return false;
            }
        }
        static bool IsDamagedByPlayerInNeutralGrid(IMyCubeGrid grid, out IMyPlayer damager)
        {
            damager = null;
            //return false;
            try
            {
                damager = grid.FindControllingPlayer();
                if (damager != null) return !damager.IsBot;

                try
                {
                    List<MyCubeBlock> cubeBlocks = grid.GetBlocks<MyCubeBlock>(x => x.BuiltBy != 0);
                    if (cubeBlocks.Count != 0)
                    {
                        long thatCunningGrieferId = cubeBlocks[0].BuiltBy;
                        damager = MyAPIGateway.Players.GetPlayerByID(thatCunningGrieferId);
                        return damager != null;
                    }

                    List<IMySlimBlock> slimBlocks = grid.GetBlocks(Selector: x => x.GetBuiltBy() != 0, BlockLimit: 50);
                    if (slimBlocks.Count == 0) return false; // We give up on this one
                    try
                    {
                        damager = MyAPIGateway.Players.GetPlayerByID(slimBlocks.First().GetBuiltBy());
                        if (damager != null)
                        {
                            grid.DebugWrite("Damage.IsDoneByPlayer.FindBuilderBySlimBlocks", $"Found damager player from slim block. Damager is {damager.DisplayName}");
                        }
                        return damager != null;
                    }
                    catch (Exception scrap)
                    {
                        AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check grid via SlimBlocks BuiltBy crashed.", scrap));
                        return false;
                    }
                }
                catch (Exception scrap)
                {
                    AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check grid via BuiltBy crashed.", scrap));
                    return false;
                }
            }
            catch (Exception scrap)
            {
                AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check neutral grid crashed", scrap));
                return false;
            }
        }

        private static bool IsDamagedByPlayerGrid(IMyCubeGrid grid, out IMyPlayer damager)
        {
            damager = null;
            try
            {
                long biggestOwner = grid.BigOwners.FirstOrDefault();
                if (biggestOwner != 0)
                {
                    damager = MyAPIGateway.Players.GetPlayerByID(biggestOwner);
                    return !damager?.IsBot ?? false;
                }
                return false;
            }
            catch (Exception scrap)
            {
                AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check grid via BigOwners crashed", scrap));
                return false;
            }
        }

        /// <summary>
        /// Determines if damage was done by player.
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="damager">Provides player who did the damage. Null if damager object is not a player.</param>
        public static bool IsDoneByPlayer(this MyDamageInformation damage, out IMyPlayer damager)
        {
            damager = null;
            try
            {
                IMyEntity attackerEntity = MyAPIGateway.Entities.GetEntityById(damage.AttackerId);
                AiSessionCore.DebugWrite("Damage.IsDoneByPlayer", $"Received damage: '{damage.Type}' from '{attackerEntity.GetType()}'", false);
                if (attackerEntity.EntityId == 0)
                {
                    AiSessionCore.DebugWrite("Damage.IsDoneByPlayer", "Attacker entity was not found.", antiSpam: false);
                    return false;
                }

                if (attackerEntity is IMyMeteor) return false;
                if (attackerEntity is IMyWarhead) return IsDamagedByPlayerWarhead(attackerEntity as IMyWarhead, out damager);
                if (attackerEntity is IMyEngineerToolBase) return IsDamagedByPlayer(attackerEntity as IMyEngineerToolBase, out damager);
                if (attackerEntity is IMyGunBaseUser) return IsDamagedByPlayer(attackerEntity as IMyGunBaseUser, out damager);

                attackerEntity = attackerEntity.GetTopMostParent();

                if (attackerEntity == null)
                {
                    AiSessionCore.DebugWrite("Damage.IsDoneByPlayer", "Cannot acquire the attacker's topmost entity", antiSpam: false);
                    return false;
                }

                
                if (!(attackerEntity is IMyCubeGrid)) return false;
                IMyCubeGrid grid = attackerEntity as IMyCubeGrid;
                if (grid.IsPirate()) return false;
                return grid.IsOwnedByNobody() ? IsDamagedByPlayerInNeutralGrid(grid, out damager) : IsDamagedByPlayerGrid(grid, out damager);
            }
            catch (Exception scrap)
            {
                DebugLog.WriteMessage($@"General Crash{Tab}Damage.AttackerId{Tab}{damage.AttackerId}{Tab}Damage.Amount{Tab}{damage.Amount}{Tab}Damage.Type{Tab}{damage.Type}");
                AiSessionCore.LogError("Damage.IsDoneByPlayer", new Exception($@"General crash --{damage.Type}----{scrap.InnerException}", scrap));
                return false;
            }
        }

        public static bool IsMeteor(this MyDamageInformation damage)
        {
            IMyEntity attackerEntity = MyAPIGateway.Entities.GetEntityById(damage.AttackerId);
            return attackerEntity is IMyMeteor;
        }

        public static bool IsThruster(this MyDamageInformation damage)
        {
            IMyEntity attackerEntity = MyAPIGateway.Entities.GetEntityById(damage.AttackerId);
            return attackerEntity is IMyThrust;
        }

/*
        public static bool IsGrid(this MyDamageInformation damage, out IMyCubeGrid grid)
        {
            grid = MyAPIGateway.Entities.GetEntityById(damage.AttackerId).GetTopMostParent() as IMyCubeGrid;
            return grid != null;
        }
*/

/*
        public static bool IsGrid(this MyDamageInformation damage)
        {
            IMyCubeGrid grid = MyAPIGateway.Entities.GetEntityById(damage.AttackerId).GetTopMostParent() as IMyCubeGrid;
            return grid != null;
        }
*/
    }
}