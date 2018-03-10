//using System;
//using System.Collections.Generic;
//using Sandbox.Game.Entities;
//using Sandbox.ModAPI;
//using VRage.Game;
//using VRage.Game.Components;
//using VRage.ModAPI;
//using VRage.Utils;
//using VRageMath;

//namespace EEM
//{
//    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
//    // ReSharper disable once ClassNeverInstantiated.Global
//    public class BuyShipMonitor : MySessionComponentBase
//    {
//        private bool _init;
//        private byte _skip;

//        public static readonly List<BuyShipSpawnData> Spawned = new List<BuyShipSpawnData>();
//        public static readonly List<BuyShip> ShopPBs = new List<BuyShip>();

//        private static readonly MyStringId MaterialWhitedot = MyStringId.GetOrCompute("WhiteDot");
//        private static readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("Square");

//        public override void UpdateAfterSimulation()
//        {
//            try
//            {
//                if(!_init)
//                {
//                    if(MyAPIGateway.Session == null)
//                        return;

//                    _init = true;

//                    MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
//                }

//                if(++_skip > 60)
//                {
//                    _skip = 0;
//                    long timeTicks = DateTime.UtcNow.Ticks;

//                    for(int i = Spawned.Count - 1; i >= 0; i--) // loop backwards to be able to remove elements mid-loop
//                    {
//                        if(Spawned[i].ExpireTime < timeTicks)
//                            Spawned.RemoveAt(i);
//                    }
//                }
//            }
//            catch(Exception e)
//            {
//                Log.Error(e);
//            }
//        }

//        public override void Draw()
//        {
//            try
//            {
//                if(!_init)
//                    return;

//                if(MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE)
//                {
//                    foreach(BuyShip logic in ShopPBs)
//                    {
//                        IMyTerminalBlock block = (IMyTerminalBlock)logic.Entity;

//                        if(block.ShowOnHUD)
//                        {
//                            Vector3D target = logic.GetSpawnPosition();
//                            Vector3D camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
//                            Vector3D dir = target - camPos;
//                            float dist = (float)dir.Normalize();

//                            if(dist > 300)
//                                continue;

//                            Vector3D pos = camPos + (dir * 0.05);
//                            MyTransparentGeometry.AddPointBillboard(MaterialWhitedot, Color.Purple * 0.75f, pos, 0.01f * (1f / dist), 0);

//                            MatrixD m = block.WorldMatrix;
//                            m.Translation = target;
//                            Color c = Color.Orange * 0.5f;
//                            MySimpleObjectDraw.DrawTransparentSphere(ref m, Constants.SpawnAreaRadius, ref c, MySimpleObjectRasterizer.Wireframe, 20, null, MaterialSquare, 0.01f);
//                        }
//                    }
//                }
//            }
//            catch(Exception e)
//            {
//                Log.Error(e);
//            }
//        }

//        protected override void UnloadData()
//        {
//            ShopPBs.Clear();
//            Spawned.Clear();

//            if(_init)
//            {
//                _init = false;
//                MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
//            }
//        }

//        private static void EntityAdded(IMyEntity ent)
//        {
//            try
//            {
//                if(Spawned.Count == 0)
//                    return;

//                MyCubeGrid grid = ent as MyCubeGrid;

//                if(grid == null || grid.IsPreview || grid.Physics == null)
//                    return;

//                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
//                {
//                    long timeTicks = DateTime.UtcNow.Ticks;
//                    BoundingSphereD vol = grid.PositionComp.WorldVolume;
//                    double radSq = vol.Radius;
//                    radSq *= radSq;
//                    Vector3D center = grid.PositionComp.WorldAABB.Center;

//                    for(int i = Spawned.Count - 1; i >= 0; i--) // loop backwards to be able to remove elements mid-loop
//                    {
//                        if(Spawned[i].ExpireTime < timeTicks)
//                        {
//                            Spawned.RemoveAt(i); // expired
//                            continue;
//                        }

//                        Vector3D pos = Spawned[i].Position;

//                        if(Vector3D.DistanceSquared(center, pos) <= radSq)
//                        {
//                            Spawned.RemoveAt(i); // no longer need this position

//                            // create the warp effect
//                            MyParticleEffect effect;

//                            if(MyParticlesManager.TryCreateParticleEffect(Constants.SpawnEffectName, out effect))
//                            {
//                                MatrixD em = grid.WorldMatrix;
//                                em.Translation = center;
//                                effect.WorldMatrix = em;
//                                effect.UserScale = (float)vol.Radius * Constants.SpawnEffectScale;
//                            }
//                            else
//                            {
//                                Log.Error("Couldn't spawn particle effect: " + Constants.SpawnEffectName);
//                            }

//                            break;
//                        }
//                    }
//                });
//            }
//            catch(Exception e)
//            {
//                Log.Error(e);
//            }
//        }
//    }
//}