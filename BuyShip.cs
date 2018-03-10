//using System;
//using System.Collections.Generic;
//using Sandbox.Common.ObjectBuilders;
//using Sandbox.Definitions;
//using Sandbox.Game.Entities;
//using Sandbox.ModAPI;
//using VRage.Collections;
//using VRage.Game;
//using VRage.Game.Components;
//using VRage.Game.Entity;
//using VRage.Game.ModAPI;
//using VRage.ModAPI;
//using VRage.ObjectBuilders;
//using VRageMath;

//namespace EEM
//{
//    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MyProgrammableBlock), false)]
//    public abstract class BuyShip : MyGameLogicComponent
//    {
//        private bool _first = true;
//        private bool _ignore;
//        private long _lastSpawned;
//        private byte _skip = 200;

//        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
//        {
//            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
//        }

//        public override void Close()
//        {
//            try
//            {
//                NeedsUpdate = MyEntityUpdateEnum.NONE;

//                BuyShipMonitor.ShopPBs.Remove(this);
//            }
//            catch(Exception e)
//            {
//                Log.Error(e);
//            }
//        }

//        public override void UpdateBeforeSimulation()
//        {
//            if(_ignore)
//                return;

//            try
//            {
//                IMyTerminalBlock block;
//                if(_first)
//                {
//                    if(MyAPIGateway.Session == null) // wait until session is ready
//                        return;

//                    _first = false;

//                    block = Entity as IMyTerminalBlock;

//                    if (block != null)
//                        _ignore = (block.CubeGrid.Physics == null || ((MyEntity) block.CubeGrid).IsPreview ||
//                                   !Constants.NpcFactions.Contains(block.GetOwnerFactionTag()));

//                    if(!_ignore)
//                        BuyShipMonitor.ShopPBs.Add(this);

//                    return;
//                }

//                if (++_skip <= 30) return;
                
//                _skip = 0;

//                long timeTicks = DateTime.UtcNow.Ticks;

//                if(timeTicks < _lastSpawned)
//                    return;

//                block = Entity as IMyTerminalBlock;

//                if(block?.DetailedInfo == null)
//                    return;

//                const string prefix = Constants.TradeEchoPrefix;

//                int startIndex = block.DetailedInfo.IndexOf(prefix, StringComparison.Ordinal);

//                if(startIndex == -1)
//                    return;

//                startIndex += prefix.Length;
//                int endIndex = block.DetailedInfo.IndexOf(prefix, startIndex, StringComparison.Ordinal);
//                string prefabName = (endIndex == -1 ? block.DetailedInfo.Substring(startIndex) : block.DetailedInfo.Substring(startIndex, endIndex - startIndex)).Trim();

//                if(string.IsNullOrEmpty(prefabName))
//                    return;

//                _lastSpawned = timeTicks + (TimeSpan.TicksPerSecond * Constants.TradeDelaySeconds);

//                MyPrefabDefinition def = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);

//                if(def?.CubeGrids == null)
//                {
//                    if (def != null)
//                    {
//                        MyDefinitionManager.Static.ReloadPrefabsFromFile(def.PrefabPath);
//                        def = MyDefinitionManager.Static.GetPrefabDefinition(def.Id.SubtypeName);
//                    }
//                }

//                if(def?.CubeGrids == null)
//                {
//                    Log.Error("Prefab '" + prefabName + "' not found!");
//                    return;
//                }

//                Vector3D position = GetSpawnPosition();
//                BoundingSphereD sphere = new BoundingSphereD(position, Constants.SpawnAreaRadius);
//                List<IMyEntity> ents = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);
//                MyCubeGrid grid = (MyCubeGrid)block.CubeGrid;
//                MyCubeGrid biggestInGroup = grid.GetBiggestGridInGroup();

//                foreach(IMyEntity myEntity in ents)
//                {
//                    MyEntity ent = (MyEntity) myEntity;
//                    // don't care about floating objects or asteroids/planets or physicsless or client side only entities blocking spawn zone
//                    if(ent is IMyFloatingObject || ent is IMyVoxelBase || ent.Physics == null || ent.IsPreview)
//                        continue;

//                    if(ent.EntityId == block.CubeGrid.EntityId)
//                        continue;

//                    MyCubeGrid g = ent as MyCubeGrid;

//                    if(g != null && g.GetBiggestGridInGroup() == biggestInGroup)
//                        continue;

//                    IMyProgrammableBlock pb = (IMyProgrammableBlock)Entity;

//                    if(!pb.TryRun(Constants.PbargFailPositionblocked))
//                    {
//                        Log.Info("WARNING: PB couldn't ran with arg '" + Constants.PbargFailPositionblocked + "' for some reason.");

//                        Vector3D camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;

//                        if(Vector3D.DistanceSquared(camPos, position) < (Constants.SpawnFailNotifyDistance * Constants.SpawnFailNotifyDistance))
//                            MyAPIGateway.Utilities.ShowNotification("Can't buy ship - jump in position blocked by something!", 5000, MyFontEnum.Red);
//                    }
//                    return;
//                }

//                // spawn and change inventories only server side as it is synchronized automatically
//                if(MyAPIGateway.Multiplayer.IsServer)
//                {
//                    SpawningOptions flags = SpawningOptions.SetNeutralOwner; // | SpawningOptions.RotateFirstCockpitTowardsDirection;
//                    List<IMyCubeGrid> grids = new List<IMyCubeGrid>();
//                    MyAPIGateway.PrefabManager.SpawnPrefab(grids, prefabName, position, block.WorldMatrix.Backward, block.WorldMatrix.Up, block.CubeGrid.Physics.LinearVelocity, Vector3.Zero, null, flags, false);

//                    // purge tagged inventories from PB's grid
//                    ListReader<MyCubeBlock> blocks = grid.GetFatBlocks();

//                    foreach(MyCubeBlock b in blocks)
//                    {
//                        MyInventoryBase inv;
//                        if (!b.TryGetInventory(out inv) || !Constants.NpcFactions.Contains(b.GetOwnerFactionTag()))
//                            continue;
//                        IMyTerminalBlock t = b as IMyTerminalBlock;

//                        if(t == null || !t.CustomName.Contains(Constants.PostspawnEmptyinventoryTag))
//                            continue;

//                        inv.GetItems().Clear();
//                    }
//                }

//                BuyShipMonitor.Spawned.Add(new BuyShipSpawnData()
//                {
//                    Position = position,
//                    ExpireTime = _lastSpawned,
//                });
                
//            }
//            catch(Exception e)
//            {
//                Log.Error(e);
//            }
//        }

//        public Vector3D GetSpawnPosition()
//        {
//            IMyTerminalBlock b = (IMyTerminalBlock)Entity;
//            MatrixD m = b.WorldMatrix;
//            return m.Translation + m.Up * Constants.SpawnRelativeOffsetUp + m.Left * Constants.SpawnRelativeOffsetLeft + m.Forward * Constants.SpawnRelativeOffsetForward;
//        }
//    }
//}