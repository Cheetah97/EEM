using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable ConditionIsAlwaysTrueOrFalse
#pragma warning disable 162

namespace EEM
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), true)]
    // ReSharper disable once UnusedMember.Global
    public class CleanupRemoteControl : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (!MyAPIGateway.Multiplayer.IsServer) // only server-side/SP
                    return;

                IMyRemoteControl remoteControl = (IMyRemoteControl)Entity;
                IMyCubeGrid grid = remoteControl.CubeGrid;

                if (grid.Physics == null || !remoteControl.IsWorking || !Constants.NpcFactions.Contains(remoteControl.GetOwnerFactionTag()))
                {
                    if (Constants.CleanupDebug)
                        Log.Info(grid.DisplayName + " (" + grid.EntityId + " @ " + grid.WorldMatrix.Translation + ") is not valid; " + (grid.Physics == null ? "Phys=null" : "Phys OK") + "; " + (remoteControl.IsWorking ? "RC OK" : "RC Not working!") + "; " + (!Constants.NpcFactions.Contains(remoteControl.GetOwnerFactionTag()) ? "Owner faction tag is not in NPC list (" + remoteControl.GetOwnerFactionTag() + ")" : "Owner Faction OK"));

                    return;
                }

                if (!remoteControl.CustomData.Contains(Constants.CleanupRcTag))
                {
                    if (Constants.CleanupDebug)
                        Log.Info(grid.DisplayName + " (" + grid.EntityId + " @ " + grid.WorldMatrix.Translation + ") RC does not contain the " + Constants.CleanupRcTag + "tag!");

                    return;
                }

                if (Constants.CleanupRcExtraTags.Length > 0)
                {
                    bool hasExtraTag = false;

                    foreach (string tag in Constants.CleanupRcExtraTags)
                    {
                        if (remoteControl.CustomData.Contains(tag))
                        {
                            hasExtraTag = true;
                            break;
                        }
                    }

                    if (!hasExtraTag)
                    {
                        if (Constants.CleanupDebug)
                            Log.Info(grid.DisplayName + " (" + grid.EntityId + " @ " + grid.WorldMatrix.Translation + ") RC does not contain one of the extra tags!");

                        return;
                    }
                }

                if (Constants.CleanupDebug)
                    Log.Info("Checking RC '" + remoteControl.CustomName + "' from grid '" + grid.DisplayName + "' (" + grid.EntityId + ") for any nearby players...");

                int rangeSq = CleanUpEem.RangeSq;
                Vector3D gridCenter = grid.WorldAABB.Center;

                if (rangeSq <= 0)
                {
                    if (Constants.CleanupDebug)
                        Log.Info("- WARNING: Range not assigned yet, ignoring grid for now.");

                    return;
                }

                // check if any player is within range of the ship
                foreach (IMyPlayer player in CleanUpEem.Players)
                {
                    if (Vector3D.DistanceSquared(player.GetPosition(), gridCenter) <= rangeSq)
                    {
                        if (Constants.CleanupDebug)
                            Log.Info(" - player '" + player.DisplayName + "' is within " + Math.Round(Math.Sqrt(rangeSq), 1) + "m of it, not removing.");

                        return;
                    }
                }

                if (Constants.CleanupDebug)
                    Log.Info(" - no player is within " + Math.Round(Math.Sqrt(rangeSq), 1) + "m of it, removing...");

                Log.Info("NPC ship '" + grid.DisplayName + "' (" + grid.EntityId + ") removed.");

                CleanUpEem.GetAttachedGrids(grid); // this gets all connected grids and places them in Exploration.grids (it clears it first)

                foreach (IMyCubeGrid g in CleanUpEem.Grids)
                {
                    g.Close(); // this only works server-side
                    Log.Info("  - subgrid '" + g.DisplayName + "' (" + g.EntityId + ") removed.");
                }

                grid.Close(); // this only works server-side
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}