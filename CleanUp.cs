using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.Exploration
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class EEM_CleanUp : MySessionComponentBase
    {
        public override void LoadData()
        {
            Log.SetUp("EEM", 531659576); // mod name and workshop ID
        }

        private bool init = false;
        private int skip = SKIP_UPDATES;
        private const int SKIP_UPDATES = 100;

        public static int rangeSq = -1;
        public static readonly List<IMyPlayer> players = new List<IMyPlayer>();
        public static readonly HashSet<IMyCubeGrid> grids = new HashSet<IMyCubeGrid>();
        public static readonly List<IMySlimBlock> blocks = new List<IMySlimBlock>(); // never filled

        public void Init()
        {
            init = true;
            Log.Init();

            MyAPIGateway.Session.SessionSettings.MaxDrones = Constants.FORCE_MAX_DRONES;
        }

        protected override void UnloadData()
        {
            init = false;
            Log.Close();

            players.Clear();
            grids.Clear();
            blocks.Clear();
        }

        public override void UpdateBeforeSimulation()
        {
            if(!MyAPIGateway.Multiplayer.IsServer) // only server-side/SP
                return;

            if(!init)
            {
                if(MyAPIGateway.Session == null)
                    return;

                Init();
            }

            if(++skip >= SKIP_UPDATES)
            {
                try
                {
                    skip = 0;

                    // the range used to check player distance from ships before removing them
                    rangeSq = Math.Max(MyAPIGateway.Session.SessionSettings.ViewDistance, Constants.CLEANUP_MIN_RANGE);
                    rangeSq *= rangeSq;

                    players.Clear();
                    MyAPIGateway.Players.GetPlayers(players);

                    if(Constants.CLEANUP_DEBUG)
                        Log.Info("player list updated; view range updated: " + Math.Round(Math.Sqrt(rangeSq), 1));
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public static void GetAttachedGrids(IMyCubeGrid grid)
        {
            grids.Clear();
            RecursiveGetAttachedGrids(grid);
        }

        private static void RecursiveGetAttachedGrids(IMyCubeGrid grid)
        {
            grid.GetBlocks(blocks, GetAttachedGridsLoopBlocks);
        }

        private static bool GetAttachedGridsLoopBlocks(IMySlimBlock slim) // should always return false!
        {
            var block = slim.FatBlock;

            if(block == null)
                return false;

            if(Constants.CLEANUP_CONNECTOR_CONNECTED)
            {
                var connector = block as IMyShipConnector;

                if(connector != null)
                {
                    var otherGrid = connector.OtherConnector?.CubeGrid;

                    if(otherGrid != null && !grids.Contains(otherGrid))
                    {
                        grids.Add(otherGrid);
                        RecursiveGetAttachedGrids(otherGrid);
                    }

                    return false;
                }
            }

            var rotorBase = block as IMyMotorStator;

            if(rotorBase != null)
            {
                var otherGrid = rotorBase.TopGrid;

                if(otherGrid != null && !grids.Contains(otherGrid))
                {
                    grids.Add(otherGrid);
                    RecursiveGetAttachedGrids(otherGrid);
                }

                return false;
            }

            var rotorTop = block as IMyMotorRotor;

            if(rotorTop != null)
            {
                var otherGrid = rotorTop.Base?.CubeGrid;

                if(otherGrid != null && !grids.Contains(otherGrid))
                {
                    grids.Add(otherGrid);
                    RecursiveGetAttachedGrids(otherGrid);
                }

                return false;
            }

            var pistonBase = block as IMyPistonBase;

            if(pistonBase != null)
            {
                var otherGrid = pistonBase.TopGrid;

                if(otherGrid != null && !grids.Contains(otherGrid))
                {
                    grids.Add(otherGrid);
                    RecursiveGetAttachedGrids(otherGrid);
                }

                return false;
            }

            var pistonTop = block as IMyPistonTop;

            if(pistonTop != null)
            {
                var otherGrid = pistonTop.Piston?.CubeGrid;

                if(otherGrid != null && !grids.Contains(otherGrid))
                {
                    grids.Add(otherGrid);
                    RecursiveGetAttachedGrids(otherGrid);
                }

                return false;
            }

            return false;
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), true)]
    public class EEM_RC : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }
        
        public override void UpdateAfterSimulation100()
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer) // only server-side/SP
                    return;

                var rc = (IMyRemoteControl)Entity;
                var grid = rc.CubeGrid;

                if(grid.Physics == null || !rc.IsWorking || !Constants.NPC_FACTIONS.Contains(rc.GetOwnerFactionTag()))
                {
                    if(Constants.CLEANUP_DEBUG)
                        Log.Info(grid.DisplayName + " (" + grid.EntityId + " @ " + grid.WorldMatrix.Translation + ") is not valid; " + (grid.Physics == null ? "Phys=null" : "Phys OK") + "; " + (rc.IsWorking ? "RC OK" : "RC Not working!") + "; " + (!Constants.NPC_FACTIONS.Contains(rc.GetOwnerFactionTag()) ? "Owner faction tag is not in NPC list (" + rc.GetOwnerFactionTag() + ")" : "Owner Faction OK"));

                    return;
                }

                if(!rc.CustomData.Contains(Constants.CLEANUP_RC_TAG))
                {
                    if(Constants.CLEANUP_DEBUG)
                        Log.Info(grid.DisplayName + " (" + grid.EntityId + " @ " + grid.WorldMatrix.Translation + ") RC does not contain the " + Constants.CLEANUP_RC_TAG + "tag!");

                    return;
                }

                if(Constants.CLEANUP_RC_EXTRA_TAGS.Length > 0)
                {
                    bool hasExtraTag = false;

                    foreach(var tag in Constants.CLEANUP_RC_EXTRA_TAGS)
                    {
                        if(rc.CustomData.Contains(tag))
                        {
                            hasExtraTag = true;
                            break;
                        }
                    }

                    if(!hasExtraTag)
                    {
                        if(Constants.CLEANUP_DEBUG)
                            Log.Info(grid.DisplayName + " (" + grid.EntityId + " @ " + grid.WorldMatrix.Translation + ") RC does not contain one of the extra tags!");

                        return;
                    }
                }

                if(Constants.CLEANUP_DEBUG)
                    Log.Info("Checking RC '" + rc.CustomName + "' from grid '" + grid.DisplayName + "' (" + grid.EntityId + ") for any nearby players...");

                var rangeSq = EEM_CleanUp.rangeSq;
                var gridCenter = grid.WorldAABB.Center;

                if(rangeSq <= 0)
                {
                    if(Constants.CLEANUP_DEBUG)
                        Log.Info("- WARNING: Range not assigned yet, ignoring grid for now.");

                    return;
                }

                // check if any player is within range of the ship
                foreach(var player in EEM_CleanUp.players)
                {
                    if(Vector3D.DistanceSquared(player.GetPosition(), gridCenter) <= rangeSq)
                    {
                        if(Constants.CLEANUP_DEBUG)
                            Log.Info(" - player '" + player.DisplayName + "' is within " + Math.Round(Math.Sqrt(rangeSq), 1) + "m of it, not removing.");

                        return;
                    }
                }

                if(Constants.CLEANUP_DEBUG)
                    Log.Info(" - no player is within " + Math.Round(Math.Sqrt(rangeSq), 1) + "m of it, removing...");

                Log.Info("NPC ship '" + grid.DisplayName + "' (" + grid.EntityId + ") removed.");

                EEM_CleanUp.GetAttachedGrids(grid); // this gets all connected grids and places them in Exploration.grids (it clears it first)

                foreach(var g in EEM_CleanUp.grids)
                {
                    g.Close(); // this only works server-side
                    Log.Info("  - subgrid '" + g.DisplayName + "' (" + g.EntityId + ") removed.");
                }

                grid.Close(); // this only works server-side
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}