using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.Exploration
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MyProgrammableBlock), false)]
    public class BuyShipPB : MyGameLogicComponent
    {
        private bool first = true;
        private bool ignore = false;
        private long lastSpawned = 0;
        private byte skip = 200;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void Close()
        {
            try
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;

                BuyShipMonitor.shopPBs.Remove(this);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if(ignore)
                return;

            try
            {
                if(first)
                {
                    if(MyAPIGateway.Session == null) // wait until session is ready
                        return;

                    first = false;

                    var block = Entity as IMyTerminalBlock;

                    ignore = (block.CubeGrid.Physics == null || (block.CubeGrid as MyEntity).IsPreview || !Constants.NPC_FACTIONS.Contains(block.GetOwnerFactionTag()));

                    if(!ignore)
                        BuyShipMonitor.shopPBs.Add(this);

                    return;
                }

                if(++skip > 30)
                {
                    skip = 0;

                    var timeTicks = DateTime.UtcNow.Ticks;

                    if(timeTicks < lastSpawned)
                        return;

                    var block = Entity as IMyTerminalBlock;

                    if(block.DetailedInfo == null)
                        return;

                    const string PREFIX = Constants.TRADE_ECHO_PREFIX;

                    var startIndex = block.DetailedInfo.IndexOf(PREFIX, StringComparison.Ordinal);

                    if(startIndex == -1)
                        return;

                    startIndex += PREFIX.Length;
                    var endIndex = block.DetailedInfo.IndexOf(PREFIX, startIndex, StringComparison.Ordinal);
                    var prefabName = (endIndex == -1 ? block.DetailedInfo.Substring(startIndex) : block.DetailedInfo.Substring(startIndex, endIndex - startIndex)).Trim();

                    if(string.IsNullOrEmpty(prefabName))
                        return;

                    lastSpawned = timeTicks + (TimeSpan.TicksPerSecond * Constants.TRADE_DELAY_SECONDS);

                    var def = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);

                    if(def == null || def.CubeGrids == null)
                    {
                        MyDefinitionManager.Static.ReloadPrefabsFromFile(def.PrefabPath);
                        def = MyDefinitionManager.Static.GetPrefabDefinition(def.Id.SubtypeName);
                    }

                    if(def == null || def.CubeGrids == null)
                    {
                        Log.Error("Prefab '" + prefabName + "' not found!");
                        return;
                    }

                    var position = GetSpawnPosition();
                    var sphere = new BoundingSphereD(position, Constants.SPAWN_AREA_RADIUS);
                    var ents = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);
                    var grid = (MyCubeGrid)block.CubeGrid;
                    var biggestInGroup = grid.GetBiggestGridInGroup();

                    foreach(MyEntity ent in ents)
                    {
                        // don't care about floating objects or asteroids/planets or physicsless or client side only entities blocking spawn zone
                        if(ent is IMyFloatingObject || ent is IMyVoxelBase || ent.Physics == null || ent.IsPreview)
                            continue;

                        if(ent.EntityId == block.CubeGrid.EntityId)
                            continue;

                        var g = ent as MyCubeGrid;

                        if(g != null && g.GetBiggestGridInGroup() == biggestInGroup)
                            continue;

                        var pb = (IMyProgrammableBlock)Entity;

                        if(!pb.TryRun(Constants.PBARG_FAIL_POSITIONBLOCKED))
                        {
                            Log.Info("WARNING: PB couldn't ran with arg '" + Constants.PBARG_FAIL_POSITIONBLOCKED + "' for some reason.");

                            var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;

                            if(Vector3D.DistanceSquared(camPos, position) < (Constants.SPAWN_FAIL_NOTIFY_DISTANCE * Constants.SPAWN_FAIL_NOTIFY_DISTANCE))
                                MyAPIGateway.Utilities.ShowNotification("Can't buy ship - jump in position blocked by something!", 5000, MyFontEnum.Red);
                        }
                        return;
                    }

                    // spawn and change inventories only server side as it is synchronized automatically
                    if(MyAPIGateway.Multiplayer.IsServer)
                    {
                        var flags = SpawningOptions.SetNeutralOwner; // | SpawningOptions.RotateFirstCockpitTowardsDirection;
                        var grids = new List<IMyCubeGrid>();
                        MyAPIGateway.PrefabManager.SpawnPrefab(grids, prefabName, position, block.WorldMatrix.Backward, block.WorldMatrix.Up, block.CubeGrid.Physics.LinearVelocity, Vector3.Zero, null, flags, false);

                        // purge tagged inventories from PB's grid
                        var blocks = grid.GetFatBlocks();
                        MyInventoryBase inv;

                        foreach(var b in blocks)
                        {
                            if(b.TryGetInventory(out inv) && Constants.NPC_FACTIONS.Contains(b.GetOwnerFactionTag()))
                            {
                                var t = b as IMyTerminalBlock;

                                if(t == null || !t.CustomName.Contains(Constants.POSTSPAWN_EMPTYINVENTORY_TAG))
                                    continue;

                                inv.GetItems().Clear();
                            }
                        }
                    }

                    BuyShipMonitor.spawned.Add(new SpawnData()
                    {
                        position = position,
                        expireTime = lastSpawned,
                    });
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public Vector3D GetSpawnPosition()
        {
            var b = (IMyTerminalBlock)Entity;
            var m = b.WorldMatrix;
            return m.Translation + m.Up * Constants.SPAWN_RELATIVE_OFFSET_UP + m.Left * Constants.SPAWN_RELATIVE_OFFSET_LEFT + m.Forward * Constants.SPAWN_RELATIVE_OFFSET_FORWARD;
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuyShipMonitor : MySessionComponentBase
    {
        private bool init = false;
        private byte skip = 0;

        public static readonly List<SpawnData> spawned = new List<SpawnData>();
        public static readonly List<BuyShipPB> shopPBs = new List<BuyShipPB>();

        private static readonly MyStringId MATERIAL_WHITEDOT = MyStringId.GetOrCompute("WhiteDot");
        private static readonly MyStringId MATERIAL_SQUARE = MyStringId.GetOrCompute("Square");

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    init = true;

                    MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
                }

                if(++skip > 60)
                {
                    skip = 0;
                    var timeTicks = DateTime.UtcNow.Ticks;

                    for(int i = spawned.Count - 1; i >= 0; i--) // loop backwards to be able to remove elements mid-loop
                    {
                        if(spawned[i].expireTime < timeTicks)
                            spawned.RemoveAt(i);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Draw()
        {
            try
            {
                if(!init)
                    return;

                if(MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE)
                {
                    foreach(var logic in shopPBs)
                    {
                        var block = (IMyTerminalBlock)logic.Entity;

                        if(block.ShowOnHUD)
                        {
                            var target = logic.GetSpawnPosition();
                            var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
                            var dir = target - camPos;
                            var dist = (float)dir.Normalize();

                            if(dist > 300)
                                continue;

                            var pos = camPos + (dir * 0.05);
                            MyTransparentGeometry.AddPointBillboard(MATERIAL_WHITEDOT, Color.Purple * 0.75f, pos, 0.01f * (1f / dist), 0);

                            var m = block.WorldMatrix;
                            m.Translation = target;
                            var c = Color.Orange * 0.5f;
                            MySimpleObjectDraw.DrawTransparentSphere(ref m, Constants.SPAWN_AREA_RADIUS, ref c, MySimpleObjectRasterizer.Wireframe, 20, null, MATERIAL_SQUARE, 0.01f);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        protected override void UnloadData()
        {
            shopPBs.Clear();
            spawned.Clear();

            if(init)
            {
                init = false;
                MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
            }
        }

        private void EntityAdded(IMyEntity ent)
        {
            try
            {
                if(spawned.Count == 0)
                    return;

                var grid = ent as MyCubeGrid;

                if(grid == null || grid.IsPreview || grid.Physics == null)
                    return;

                MyAPIGateway.Utilities.InvokeOnGameThread(delegate ()
                {
                    var timeTicks = DateTime.UtcNow.Ticks;
                    var vol = grid.PositionComp.WorldVolume;
                    var radSq = vol.Radius;
                    radSq *= radSq;
                    var center = grid.PositionComp.WorldAABB.Center;

                    for(int i = spawned.Count - 1; i >= 0; i--) // loop backwards to be able to remove elements mid-loop
                    {
                        if(spawned[i].expireTime < timeTicks)
                        {
                            spawned.RemoveAt(i); // expired
                            continue;
                        }

                        var pos = spawned[i].position;

                        if(Vector3D.DistanceSquared(center, pos) <= radSq)
                        {
                            spawned.RemoveAt(i); // no longer need this position

                            // create the warp effect
                            MyParticleEffect effect;

                            if(MyParticlesManager.TryCreateParticleEffect(Constants.SPAWN_EFFECT_NAME, out effect))
                            {
                                var em = grid.WorldMatrix;
                                em.Translation = center;
                                effect.WorldMatrix = em;
                                effect.UserScale = (float)vol.Radius * Constants.SPAWN_EFFECT_SCALE;
                            }
                            else
                            {
                                Log.Error("Couldn't spawn particle effect: " + Constants.SPAWN_EFFECT_NAME);
                            }

                            break;
                        }
                    }
                });
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public struct SpawnData
    {
        public Vector3D position;
        public long expireTime;
    }
}