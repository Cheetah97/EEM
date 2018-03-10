using System;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using Sandbox.Game.EntityComponents;
using VRage.Game.Entity;
using Sandbox.Game;
using VRageMath;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using System.Collections.Generic;
using Sandbox.ModAPI.Interfaces;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Weapons;

namespace Cheetah.AI
{
    public static class OwnershipTools
    {
        public static long PirateID
        {
            get
            {
                return MyVisualScriptLogicProvider.GetPirateId();
            }
        }

        public static bool IsOwnedByPirates(this IMyTerminalBlock Block)
        {
            return Block.OwnerId == PirateID;
        }

        public static bool IsOwnedByNPC(this IMyTerminalBlock Block, bool AllowNobody = true, bool CheckBuilder = false)
        {
            if (!CheckBuilder)
            {
                if (Block.IsOwnedByPirates()) return true;
                if (!AllowNobody && Block.IsOwnedByNobody()) return false;
                IMyPlayer Owner = MyAPIGateway.Players.GetPlayerByID(Block.OwnerId);
                return Owner != null ? Owner.IsBot : true;
            }
            else
            {
                if (!Block.IsOwnedByNPC(AllowNobody)) return false;
                long BuilderID = Block.GetBuiltBy();
                if (!AllowNobody && BuilderID == 0) return false;
                IMyPlayer Owner = MyAPIGateway.Players.GetPlayerByID(BuilderID);
                return Owner != null ? Owner.IsBot : true;
            }
        }

        public static bool IsPirate(this IMyCubeGrid Grid, bool StrictCheck = false)
        {
            if (Grid.BigOwners.Count == 0 || Grid.BigOwners[0] == 0) return false;
            if (!StrictCheck) return Grid.BigOwners.Contains(PirateID);
            else
            {
                return Grid.BigOwners.Count == 1 && Grid.BigOwners[0] == PirateID;
            }
        }

        public static bool IsNPC(this IMyCubeGrid Grid)
        {
            if (Grid.IsPirate()) return true;
            if (Grid.BigOwners.Count == 0) return false;
            IMyPlayer Owner = MyAPIGateway.Players.GetPlayerByID(Grid.BigOwners[0]);
            return Owner != null ? Owner.IsBot : true;
        }

        public static bool IsOwnedByNobody(this IMyCubeGrid Grid)
        {
            return Grid.BigOwners.Count == 0 || Grid.BigOwners[0] == 0;
        }

        public static bool IsOwnedByNobody(this IMyCubeBlock Block)
        {
            return Block.OwnerId == 0;
        }

        public static bool IsBuiltByNobody(this IMyCubeBlock Block)
        {
            return Block.GetBuiltBy() == 0;
        }

        public static bool IsPlayerBlock(this IMySlimBlock Block, out IMyPlayer Builder)
        {
            Builder = null;
            long BuiltBy = Block.GetBuiltBy();
            if (BuiltBy == 0) return false;
            Builder = MyAPIGateway.Players.GetPlayerByID(BuiltBy);
            return Builder != null && !Builder.IsBot;
        }

        public static bool IsPlayerBlock(this IMyCubeBlock Block, out IMyPlayer Owner)
        {
            Owner = null;
            if (Block.OwnerId != 0)
            {
                return MyAPIGateway.Players.IsValidPlayer(Block.OwnerId, out Owner);
            }
            else
            {
                long BuiltBy = Block.GetBuiltBy();
                if (BuiltBy == 0) return false;
                Owner = MyAPIGateway.Players.GetPlayerByID(BuiltBy);
                return Owner != null && !Owner.IsBot;
            }
        }
    }

    public static class TerminalExtensions
    {
        public static IMyGridTerminalSystem GetTerminalSystem(this IMyCubeGrid Grid)
        {
            return MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid);
        }

        /// <summary>
        /// Allows GetBlocksOfType to work like a chainable function.
        /// <para />
        /// Enjoy allocating.
        /// </summary>
        public static List<T> GetBlocksOfType<T>(this IMyGridTerminalSystem Term, Func<T, bool> collect = null) where T : class, Sandbox.ModAPI.Ingame.IMyTerminalBlock
        {
            List<T> TermBlocks = new List<T>();
            Term.GetBlocksOfType<T>(TermBlocks, collect);
            return TermBlocks;
        }

        public static void Trigger(this IMyTimerBlock Timer)
        {
            Timer.GetActionWithName("TriggerNow").Apply(Timer);
        }

        public static List<IMyInventory> GetInventories(this IMyEntity Entity)
        {
            if (!Entity.HasInventory) return new List<IMyInventory>();

            List<IMyInventory> Inventories = new List<IMyInventory>();
            for (int i=0; i<Entity.InventoryCount; i++)
            {
                Inventories.Add(Entity.GetInventory(i));
            }
            return Inventories;
        }
    }

    public static class VectorExtensions
    {
        public static double DistanceTo(this Vector3D From, Vector3D To)
        {
            return (To - From).Length();
        }

        public static Vector3D LineTowards(this Vector3D From, Vector3D To, double Length)
        {
            return From + (Vector3D.Normalize(To - From) * Length);
        }

        public static Vector3D InverseVectorTo(this Vector3D From, Vector3D To, double Length)
        {
            return From + (Vector3D.Normalize(From - To) * Length);
        }
    }

    public static class GridExtenstions
    {
        /// <summary>
        /// Returns world speed cap, in m/s.
        /// </summary>
        public static float GetSpeedCap(this IMyShipController ShipController)
        {
            if (ShipController.CubeGrid.GridSizeEnum == MyCubeSize.Small) return MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
            if (ShipController.CubeGrid.GridSizeEnum == MyCubeSize.Large) return MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed;
            return 100;
        }

        /// <summary>
        /// Returns world speed cap ratio to default cap of 100 m/s.
        /// </summary>
        public static float GetSpeedCapRatioToDefault(this IMyShipController ShipController)
        {
            return ShipController.GetSpeedCap() / 100;
        }

        public static IMyPlayer FindControllingPlayer(this IMyCubeGrid Grid, bool Write = true)
        {
            try
            {
                IMyPlayer Player = null;
                IMyGridTerminalSystem Term = Grid.GetTerminalSystem();
                List<IMyShipController> ShipControllers = Term.GetBlocksOfType<IMyShipController>(collect: x => x.IsUnderControl);
                if (ShipControllers.Count == 0)
                {
                    ShipControllers = Term.GetBlocksOfType<IMyShipController>(x => x.GetBuiltBy() != 0);
                    if (ShipControllers.Count > 0)
                    {
                        IMyShipController MainController = ShipControllers.FirstOrDefault(x => x.IsMainCockpit()) ?? ShipControllers.First();
                        long ID = MainController.GetBuiltBy();
                        Player = MyAPIGateway.Players.GetPlayerByID(ID);
                        if (Write && Player != null) Grid.DebugWrite("Grid.FindControllingPlayer", $"Found cockpit built by player {Player.DisplayName}.");
                        return Player;
                    }
                    if (Write) Grid.DebugWrite("Grid.FindControllingPlayer", "No builder player was found.");
                    return null;
                }

                Player = MyAPIGateway.Players.GetPlayerByID(ShipControllers.First().ControllerInfo.ControllingIdentityId);
                if (Write && Player != null) Grid.DebugWrite("Grid.FindControllingPlayer", $"Found player in control: {Player.DisplayName}");
                return Player;
            }
            catch (Exception Scrap)
            {
                Grid.LogError("Grid.FindControllingPlayer", Scrap);
                return null;
            }
        }

        public static bool HasCockpit(this IMyCubeGrid Grid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            Grid.GetBlocks(blocks, x => x is IMyCockpit);
            return blocks.Count > 0;
        }

        public static bool HasRemote(this IMyCubeGrid Grid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            Grid.GetBlocks(blocks, x => x is IMyRemoteControl);
            return blocks.Count > 0;
        }

        public static bool HasShipController(this IMyCubeGrid Grid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            Grid.GetBlocks(blocks, x => x is IMyShipController);
            return blocks.Count > 0;
        }

        public static IMyFaction GetOwnerFaction(this IMyCubeGrid Grid, bool RecalculateOwners = false)
        {
            try
            {
                if (RecalculateOwners)
                    (Grid as MyCubeGrid).RecalculateOwners();

                IMyFaction FactionFromBigowners = null;
                IMyFaction Faction = null;
                if (Grid.BigOwners.Count > 0 && Grid.BigOwners[0] != 0)
                {
                    long OwnerID = Grid.BigOwners[0];
                    FactionFromBigowners = GeneralExtensions.FindOwnerFactionById(OwnerID);
                }
                else
                {
                    Grid.LogError("Grid.GetOwnerFaction", new Exception("Cannot get owner faction via BigOwners.", new Exception("BigOwners is empty.")));
                }

                IMyGridTerminalSystem Term = Grid.GetTerminalSystem();
                List<IMyTerminalBlock> AllTermBlocks = new List<IMyTerminalBlock>();
                Term.GetBlocks(AllTermBlocks);

                if (AllTermBlocks.Empty())
                {
                    Grid.DebugWrite("Grid.GetOwnerFaction", $"Terminal system is empty!");
                    return null;
                }

                var BiggestOwnerGroup = AllTermBlocks.GroupBy(x => x.GetOwnerFactionTag()).OrderByDescending(gp => gp.Count()).FirstOrDefault();
                if (BiggestOwnerGroup != null)
                {
                    string factionTag = BiggestOwnerGroup.Key;
                    Faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(factionTag);
                    if (Faction != null)
                        Grid.DebugWrite("Grid.GetOwnerFaction", $"Found owner faction {factionTag} via terminal system");
                    return Faction ?? FactionFromBigowners;
                }
                else
                {
                    Grid.DebugWrite("Grid.GetOwnerFaction", $"CANNOT GET FACTION TAGS FROM TERMINALSYSTEM!");
                    List<IMyShipController> Controllers = Grid.GetBlocks<IMyShipController>();
                    if (Controllers.Any())
                    {
                        List<IMyShipController> MainControllers;

                        if (Controllers.Any(x => x.IsMainCockpit(), out MainControllers))
                        {
                            Faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(MainControllers[0].GetOwnerFactionTag());
                            if (Faction != null)
                            {
                                Grid.DebugWrite("Grid.GetOwnerFaction", $"Found owner faction {Faction.Tag} via main cockpit");
                                return Faction ?? FactionFromBigowners;
                            }
                        } // Controls falls down if faction was not found by main cockpit

                        Faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(Controllers[0].GetOwnerFactionTag());
                        if (Faction != null)
                        {
                            Grid.DebugWrite("Grid.GetOwnerFaction", $"Found owner faction {Faction.Tag} via cockpit");
                            return Faction ?? FactionFromBigowners;
                        }
                        else
                        {
                            Grid.DebugWrite("Grid.GetOwnerFaction", $"Unable to owner faction via cockpit!");
                            Faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(AllTermBlocks.First().GetOwnerFactionTag());
                            if (Faction != null)
                            {
                                Grid.DebugWrite("Grid.GetOwnerFaction", $"Found owner faction {Faction.Tag} via first terminal block");
                                return Faction ?? FactionFromBigowners;
                            }
                            else
                            {
                                Grid.DebugWrite("Grid.GetOwnerFaction", $"Unable to owner faction via first terminal block!");
                                return Faction ?? FactionFromBigowners;
                            }
                        }
                    }
                    else
                    {
                        Faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(AllTermBlocks.First().GetOwnerFactionTag());
                        if (Faction != null)
                        {
                            Grid.DebugWrite("Grid.GetOwnerFaction", $"Found owner faction {Faction.Tag} via first terminal block");
                            return Faction ?? FactionFromBigowners;
                        }
                        else
                        {
                            Grid.DebugWrite("Grid.GetOwnerFaction", $"Unable to owner faction via first terminal block!");
                            return Faction ?? FactionFromBigowners;
                        }
                    }
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("Faction.GetOwnerFaction", Scrap);
                return null;
            }
        }

        public static List<T> GetBlocks<T>(this IMyCubeGrid Grid, Func<T, bool> Selector = null) where T : class, IMyEntity
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            List<T> Blocks = new List<T>();
            Grid.GetBlocks(blocks, x => x is T);
            foreach (IMySlimBlock block in blocks)
            {
                T Block = block as T;
                // Not the most efficient method, but GetBlocks only allows IMySlimBlock selector
                if (Selector == null || Selector(Block))
                    Blocks.Add(Block);
            }
            return Blocks;
        }

        public static List<IMySlimBlock> GetBlocks(this IMyCubeGrid Grid, Func<IMySlimBlock, bool> Selector = null, int BlockLimit = 0)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            int i = 0;
            Func<IMySlimBlock, bool> Collector = Selector;
            if (BlockLimit > 0)
            {
                Collector = (Block) =>
                {
                    if (i >= BlockLimit) return false;
                    i++;
                    if (Selector != null) return Selector(Block);
                    return true;
                };
            }

            if (Collector == null)
                Grid.GetBlocks(blocks);
            else
                Grid.GetBlocks(blocks, Collector);
            return blocks;
        }

        /// <summary>
        /// Remember, this is only for server-side.
        /// </summary>
        public static void ChangeOwnershipSmart(this IMyCubeGrid Grid, long NewOwnerID, MyOwnershipShareModeEnum ShareMode)
        {
            if (!MyAPIGateway.Session.IsServer) return;
            try
            {
                var Subgrids = Grid.GetAllSubgrids();
                Grid.ChangeGridOwnership(NewOwnerID, ShareMode);
                foreach (IMyCubeGrid Subgrid in Subgrids)
                {
                    try
                    {
                        Subgrid.ChangeGridOwnership(NewOwnerID, ShareMode);
                    }
                    catch (Exception Scrap)
                    {
                        Grid.LogError("ChangeOwnershipSmart.ChangeSubgridOwnership", Scrap);
                    }
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("ChangeOwnershipSmart", Scrap);
            }
        }

        public static void DeleteSmart(this IMyCubeGrid Grid)
        {
            if (!MyAPIGateway.Session.IsServer) return;
            List<IMyCubeGrid> Subgrids = Grid.GetAllSubgrids();
            foreach (IMyCubeGrid Subgrid in Subgrids)
                Subgrid.Close();
            Grid.Close();
        }

        public static List<IMyCubeGrid> GetAllSubgrids(this IMyCubeGrid Grid)
        {
            try
            {
                return MyAPIGateway.GridGroups.GetGroup(Grid, GridLinkTypeEnum.Logical);
            }
            catch (Exception Scrap)
            {
                Grid.LogError("GetAllSubgrids", Scrap);
                return new List<IMyCubeGrid>();
            }
        }
    }

    public static class FactionsExtensions
    {
        public static void DeclareWar(this IMyFaction OurFaction, IMyFaction HostileFaction, bool Print = false)
        {
            MyAPIGateway.Session.Factions.DeclareWar(OurFaction.FactionId, HostileFaction.FactionId);
            if (Print) AISessionCore.DebugWrite($"{OurFaction.Tag}", $"Declared war on {HostileFaction.Tag}", AntiSpam: false);
        }

        public static void ProposePeace(this IMyFaction OurFaction, IMyFaction HostileFaction, bool Print = false)
        {
            MyAPIGateway.Session.Factions.SendPeaceRequest(OurFaction.FactionId, HostileFaction.FactionId);
            if (Print) AISessionCore.DebugWrite($"{OurFaction.Tag}", $"Proposed peace to {HostileFaction.Tag}", AntiSpam: false);
        }

        public static void AcceptPeace(this IMyFaction OurFaction, IMyFaction HostileFaction, bool Print = false)
        {
            MyAPIGateway.Session.Factions.AcceptPeace(HostileFaction.FactionId, OurFaction.FactionId);
            MyAPIGateway.Session.Factions.AcceptPeace(OurFaction.FactionId, HostileFaction.FactionId);
            if (Print) AISessionCore.DebugWrite($"{OurFaction.Tag}", $"Accepted peace from {HostileFaction.Tag}", AntiSpam: false);
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

    public static class DamageHelper
    {
        /// <summary>
        /// Determines if damage was done by player.
        /// <para/>
        /// If it's necessary to determine who did the damage, use overload.
        /// </summary>
        public static bool IsDoneByPlayer(this MyDamageInformation Damage)
        {
            IMyPlayer trash;
            return Damage.IsDoneByPlayer(out trash);
        }

        static bool IsDamagedByPlayerWarhead(IMyWarhead Warhead, out IMyPlayer Damager)
        {
            Damager = null;
            try
            {
                if (Warhead.OwnerId == 0)
                {
                    Damager = MyAPIGateway.Players.GetPlayerByID((Warhead as MyCubeBlock).BuiltBy);
                    AISessionCore.DebugWrite("Damage.IsDoneByPlayer", "Attempting to find damager by neutral warhead.");
                    return Damager != null;
                }
                else
                {
                    Damager = MyAPIGateway.Players.GetPlayerByID(Warhead.OwnerId);
                    AISessionCore.DebugWrite("Damage.IsDoneByPlayer", "Attempting to find damager by warhead owner.");
                    return Damager != null;
                }
            }
            catch (Exception Scrap)
            {
                AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check for neutral warheads crashed", Scrap));
                return false;
            }
        }

        static bool IsDamagedByPlayer(IMyGunBaseUser Gun, out IMyPlayer Damager)
        {
            Damager = null;
            try
            {
                Damager = MyAPIGateway.Players.GetPlayerByID(Gun.OwnerId);
                //AISessionCore.DebugWrite($"GunDamage.IsDamagedByPlayer", $"Getting player from gun. ID: {Gun.OwnerId}, player: {(Damager != null ? Damager.DisplayName : "null")}", false);
                return Damager != null ? !Damager.IsBot : false;
            }
            catch (Exception Scrap)
            {
                AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check gun owner crashed", Scrap));
                return false;
            }
        }
        static bool IsDamagedByPlayer(IMyEngineerToolBase Tool, out IMyPlayer Damager)
        {
            Damager = null;
            try
            {
                Damager = MyAPIGateway.Players.GetPlayerByID(Tool.OwnerIdentityId);
                //AISessionCore.DebugWrite($"ToolDamage.IsDamagedByPlayer", $"Getting player from tool. ID: {Tool.OwnerId}, IdentityID: {Tool.OwnerIdentityId}, player: {(Damager != null ? Damager.DisplayName : "null")}", false);
                return Damager != null ? !Damager.IsBot : false;
            }
            catch (Exception Scrap)
            {
                AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check gun owner crashed", Scrap));
                return false;
            }
        }
        static bool IsDamagedByPlayerInNeutralGrid(IMyCubeGrid Grid, out IMyPlayer Damager)
        {
            Damager = null;
            try
            {
                Damager = Grid.FindControllingPlayer();
                if (Damager != null) return !Damager.IsBot;

                try
                {
                    List<MyCubeBlock> CubeBlocks = Grid.GetBlocks<MyCubeBlock>(x => x.BuiltBy != 0);
                    if (CubeBlocks.Count != 0)
                    {
                        var ThatCunningGrieferID = CubeBlocks[0].BuiltBy;
                        Damager = MyAPIGateway.Players.GetPlayerByID(ThatCunningGrieferID);
                        return Damager != null;
                    }
                    else
                    {
                        List<IMySlimBlock> SlimBlocks = Grid.GetBlocks(Selector: x => x.GetBuiltBy() != 0, BlockLimit: 50);
                        if (SlimBlocks.Count == 0) return false; // We give up on this one
                        else
                        {
                            try
                            {
                                Damager = MyAPIGateway.Players.GetPlayerByID(SlimBlocks.First().GetBuiltBy());
                                if (Damager != null)
                                {
                                    Grid.DebugWrite("Damage.IsDoneByPlayer.FindBuilderBySlimBlocks", $"Found damager player from slim block. Damager is {Damager.DisplayName}");
                                }
                                return Damager != null;
                            }
                            catch (Exception Scrap)
                            {
                                AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check grid via SlimBlocks BuiltBy crashed.", Scrap));
                                return false;
                            }
                        }
                    }
                }
                catch (Exception Scrap)
                {
                    AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check grid via BuiltBy crashed.", Scrap));
                    return false;
                }
            }
            catch (Exception Scrap)
            {
                AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check neutral grid crashed", Scrap));
                return false;
            }
        }
        static bool IsDamagedByPlayerGrid(IMyCubeGrid Grid, out IMyPlayer Damager)
        {
            Damager = null;
            try
            {
                long BiggestOwner = Grid.BigOwners.FirstOrDefault();
                if (BiggestOwner != 0)
                {
                    Damager = MyAPIGateway.Players.GetPlayerByID(BiggestOwner);
                    return Damager != null ? !Damager.IsBot : false;
                }
                else return false;
            }
            catch (Exception Scrap)
            {
                AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("Check grid via BigOwners crashed", Scrap));
                return false;
            }
        }


        /// <summary>
        /// Determines if damage was done by player.
        /// </summary>
        /// <param name="Damager">Provides player who did the damage. Null if damager object is not a player.</param>
        public static bool IsDoneByPlayer(this MyDamageInformation Damage, out IMyPlayer Damager)
        {
            Damager = null;
            try
            {
                IMyEntity AttackerEntity = MyAPIGateway.Entities.GetEntityById(Damage.AttackerId);
                AISessionCore.DebugWrite("Damage.IsDoneByPlayer", $"Received damage: '{Damage.Type.ToString()}' from '{AttackerEntity.GetType().ToString()}'", false);
                if (AttackerEntity == null)
                {
                    AISessionCore.DebugWrite("Damage.IsDoneByPlayer", "Attacker entity was not found.", AntiSpam: false);
                    return false;
                }
                
                if (AttackerEntity is IMyMeteor) return false;
                if (AttackerEntity is IMyWarhead) return IsDamagedByPlayerWarhead(AttackerEntity as IMyWarhead, out Damager);
                if (AttackerEntity is IMyEngineerToolBase) return IsDamagedByPlayer(AttackerEntity as IMyEngineerToolBase, out Damager);
                if (AttackerEntity is IMyGunBaseUser) return IsDamagedByPlayer(AttackerEntity as IMyGunBaseUser, out Damager);

                AttackerEntity = AttackerEntity.GetTopMostParent();

                if (AttackerEntity == null)
                {
                    AISessionCore.DebugWrite("Damage.IsDoneByPlayer", "Cannot acquire the attacker's topmost entity", AntiSpam: false);
                    return false;
                }

                if (AttackerEntity is IMyCubeGrid)
                {
                    IMyCubeGrid Grid = AttackerEntity as IMyCubeGrid;
                    if (Grid.IsPirate()) return false;
                    if (Grid.IsOwnedByNobody()) return IsDamagedByPlayerInNeutralGrid(Grid, out Damager);

                    return IsDamagedByPlayerGrid(Grid, out Damager);
                }

                return false;
            }
            catch (Exception Scrap)
            {
                AISessionCore.LogError("Damage.IsDoneByPlayer", new Exception("General crash.", Scrap));
                return false;
            }
        }

        public static bool IsMeteor(this MyDamageInformation Damage)
        {
            IMyEntity AttackerEntity = MyAPIGateway.Entities.GetEntityById(Damage.AttackerId);
            return AttackerEntity is IMyMeteor;
        }

        public static bool IsThruster(this MyDamageInformation Damage)
        {
            IMyEntity AttackerEntity = MyAPIGateway.Entities.GetEntityById(Damage.AttackerId);
            return AttackerEntity is IMyThrust;
        }

        public static bool IsGrid(this MyDamageInformation Damage, out IMyCubeGrid Grid)
        {
            Grid = MyAPIGateway.Entities.GetEntityById(Damage.AttackerId).GetTopMostParent() as IMyCubeGrid;
            return Grid != null;
        }

        public static bool IsGrid(this MyDamageInformation Damage)
        {
            var Grid = MyAPIGateway.Entities.GetEntityById(Damage.AttackerId).GetTopMostParent() as IMyCubeGrid;
            return Grid != null;
        }
    }

    public static class InventoryHelpers
    {
        public static MyDefinitionId GetBlueprint(this IMyInventoryItem Item)
        {
            return new MyDefinitionId(Item.Content.TypeId, Item.Content.SubtypeId);
        }

        public static bool IsOfType(this MyDefinitionId Id, string Type)
        {
            return Id.TypeId.ToString() == Type || Id.TypeId.ToString() == "MyObjectBuilder_" + Type;
        }

        public static bool IsOfType(this MyObjectBuilder_Base Id, string Type)
        {
            return Id.TypeId.ToString() == Type || Id.TypeId.ToString() == "MyObjectBuilder_" + Type;
        }

        public static bool IsOfType(this IMyInventoryItem Item, string Type)
        {
            return Item.Content.IsOfType(Type);
        }
    }

    public class EntityByDistanceSorter : IComparer<IMyEntity>, IComparer<IMySlimBlock>, IComparer<Sandbox.ModAPI.Ingame.MyDetectedEntityInfo>
    {
        public Vector3D Position { get; set; }
        public EntityByDistanceSorter(Vector3D Position)
        {
            this.Position = Position;
        }

        public int Compare(IMyEntity x, IMyEntity y)
        {
            var DistanceX = Vector3D.DistanceSquared(Position, x.GetPosition());
            var DistanceY = Vector3D.DistanceSquared(Position, y.GetPosition());

            if (DistanceX < DistanceY) return -1;
            if (DistanceX > DistanceY) return 1;
            return 0;
        }

        public int Compare(Sandbox.ModAPI.Ingame.MyDetectedEntityInfo x, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo y)
        {
            var DistanceX = Vector3D.DistanceSquared(Position, x.Position);
            var DistanceY = Vector3D.DistanceSquared(Position, y.Position);

            if (DistanceX < DistanceY) return -1;
            if (DistanceX > DistanceY) return 1;
            return 0;
        }

        public int Compare(IMySlimBlock x, IMySlimBlock y)
        {
            var DistanceX = Vector3D.DistanceSquared(Position, x.CubeGrid.GridIntegerToWorld(x.Position));
            var DistanceY = Vector3D.DistanceSquared(Position, y.CubeGrid.GridIntegerToWorld(y.Position));

            if (DistanceX < DistanceY) return -1;
            if (DistanceX > DistanceY) return 1;
            return 0;
        }
    }

    /// <summary>
    /// Provides a set of methods to fix some of the LINQ idiocy.
    /// <para/>
    /// Enjoy your allocations.
    /// </summary>
    public static class GenericHelpers
    {
        public static List<T> Except<T>(this List<T> Source, Func<T, bool> Sorter)
        {
            return Source.Where(x => !Sorter(x)).ToList();
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> Source)
        {
            HashSet<T> Hashset = new HashSet<T>();
            foreach (T item in Source)
                Hashset.Add(item);
            return Hashset;
        }

        /// <summary>
        /// Returns a list with one item excluded.
        /// </summary>
        public static List<T> Except<T>(this List<T> Source, T exclude)
        {
            return Source.Where(x => !x.Equals(exclude)).ToList();
        }

        public static bool Any<T>(this IEnumerable<T> Source, Func<T, bool> Sorter, out IEnumerable<T> Any)
        {
            Any = Source.Where(Sorter);
            return Any.Count() > 0;
        }

        /// <summary>
        /// Determines if the sequence has no elements matching a given predicate.
        /// <para />
        /// Basically, it's an inverted Any().
        /// </summary>
        public static bool None<T>(this IEnumerable<T> Source, Func<T, bool> Sorter)
        {
            return !Source.Any(Sorter);
        }

        public static IEnumerable<T> Unfitting<T>(this IEnumerable<T> Source, Func<T, bool> Sorter)
        {
            return Source.Where(x => Sorter(x) == false);
        }

        public static List<T> Unfitting<T>(this List<T> Source, Func<T, bool> Sorter)
        {
            return Source.Where(x => Sorter(x) == false).ToList();
        }

        public static bool Any<T>(this List<T> Source, Func<T, bool> Sorter, out List<T> Any)
        {
            Any = Source.Where(Sorter).ToList();
            return Any.Count > 0;
        }

        public static bool Empty<T>(this IEnumerable<T> Source)
        {
            return Source.Count() == 0;
        }
    }

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
            var builder = Block.GetObjectBuilder();
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
                    foreach (var member in Faction.Members)
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
                AISessionCore.LogError("Faction.IsNPC", Scrap);
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
            var Factions = MyAPIGateway.Session.Factions.Factions.Values;
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

    public static class NumberExtensions
    {
        public static int Squared(this int Num)
        {
            return (int)Math.Pow(Num, 2);
        }

        public static int Cubed(this int Num)
        {
            return (int)Math.Pow(Num, 3);
        }

        public static float Squared(this float Num)
        {
            return (float)Math.Pow(Num, 2);
        }

        public static float Cubed(this float Num)
        {
            return (float)Math.Pow(Num, 3);
        }

        public static float Root(this float Num)
        {
            return (float)Math.Sqrt(Num);
        }

        public static float Cuberoot(this float Num)
        {
            return (float)Math.Pow(Num, 1 / 3);
        }

        public static double Squared(this double Num)
        {
            return Math.Pow(Num, 2);
        }

        public static double Cubed(this double Num)
        {
            return Math.Pow(Num, 3);
        }

        public static double Root(this double Num)
        {
            return Math.Sqrt(Num);
        }

        public static double Cuberoot(this double Num)
        {
            return Math.Pow(Num, 1 / 3);
        }
    }

    public static class DebugHelper
    {
        private static readonly List<int> AlreadyPostedMessages = new List<int>();

        public static void Print(string Source, string Message, bool AntiSpam = true)
        {
            string combined = Source + ": " + Message;
            int hash = combined.GetHashCode();

            if (!AlreadyPostedMessages.Contains(hash))
            {
                AlreadyPostedMessages.Add(hash);
                MyAPIGateway.Utilities.ShowMessage(Source, Message);
                VRage.Utils.MyLog.Default.WriteLine(Source + $": Debug message: {Message}");
                VRage.Utils.MyLog.Default.Flush();
            }
        }

        public static void DebugWrite(this IMyCubeGrid Grid, string Source, string Message, bool AntiSpam = true, bool ForceWrite = false)
        {
            if (AISessionCore.Debug || ForceWrite) Print(Grid.DisplayName, $"Debug message from '{Source}': {Message}");
        }

        public static void LogError(this IMyCubeGrid Grid, string Source, Exception Scrap, bool AntiSpam = true, bool ForceWrite = false)
        {
            if (!AISessionCore.Debug && !ForceWrite) return;
            string DisplayName = "Unknown Grid";
            try
            {
                DisplayName = Grid.DisplayName;
            }
            finally
            {
                Print(DisplayName, $"Fatal error in '{Source}': {Scrap.Message}. {(Scrap.InnerException != null ? Scrap.InnerException.Message : "No additional info was given by the game :(")}");
            }
        }
    }
}