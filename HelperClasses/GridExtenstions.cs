using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace EEM.HelperClasses
{
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

                IGrouping<string, IMyTerminalBlock> BiggestOwnerGroup = AllTermBlocks.GroupBy(x => x.GetOwnerFactionTag()).OrderByDescending(gp => gp.Count()).FirstOrDefault();
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
            try
            {
                List<IMyCubeGrid> Subgrids = Grid.GetAllSubgrids();
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
}