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
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Interfaces;
using System.Linq;
using Ingame = Sandbox.ModAPI.Ingame;


namespace Cheetah.AI
{
    public sealed class FreighterBot : BotBase
    {
        static public readonly BotTypes BotType = BotTypes.Freighter;
        FreighterSettings FreighterSetup;
        struct FreighterSettings
        {
            public bool FleeOnlyWhenDamaged;
            public float FleeTriggerDistance;
            public float FleeSpeedRatio;
            public float FleeSpeedCap;
            public float CruiseSpeed;

            public void Default()
            {
                if (FleeOnlyWhenDamaged == default(bool)) FleeOnlyWhenDamaged = false;
                if (FleeTriggerDistance == default(float)) FleeTriggerDistance = 1000;
                if (FleeSpeedRatio == default(float)) FleeSpeedRatio = 1.0f;
                if (FleeSpeedCap == default(float)) FleeSpeedCap = 300;
            }
        }
        private bool IsFleeing = false;
        private bool FleeTimersTriggered = false;
        
        public FreighterBot(IMyCubeGrid Grid) : base(Grid)
        {
        }

        public override bool Init(IMyRemoteControl RC = null)
        {
            if (!base.Init(RC)) return false;
            OnDamaged += DamageHandler;
            OnBlockPlaced += BlockPlacedHandler;

            SetFlightPath();

            Update |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            return true;
        }

        void SetFlightPath()
        {
            Vector3D Velocity = RC.GetShipVelocities().LinearVelocity;
            float Speed = (float)RC.GetShipSpeed();
            Vector3D Endpoint;
            if (Speed > 5)
            {
                Endpoint = GridPosition + (Vector3D.Normalize(Velocity) * 1000000);
            }
            else
            {
                Endpoint = GridPosition + (RC.WorldMatrix.Forward * 1000000);
            }

            if (FreighterSetup.CruiseSpeed != default(float))
                (RC as MyRemoteControl).SetAutoPilotSpeedLimit(FreighterSetup.CruiseSpeed);
            else
                (RC as MyRemoteControl).SetAutoPilotSpeedLimit(Speed > 5 ? Speed : 30);

            (RC as MyRemoteControl).SetCollisionAvoidance(true);
        }

        private void DamageHandler(IMySlimBlock Block, MyDamageInformation Damage)
        {
            try
            {
                IMyPlayer Damager;
                ReactOnDamage(Block, Damage, CalmdownTime, out Damager);
                if (Damager != null)
                {
                    IsFleeing = true;
                    Flee();
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("DamageHandler", Scrap);
            }
        }

        public override void Main()
        {
            if (IsFleeing) Flee();
            else if (!FreighterSetup.FleeOnlyWhenDamaged)
            {
                List<Ingame.MyDetectedEntityInfo> EnemiesAround = LookForEnemies(FreighterSetup.FleeTriggerDistance, ConsiderNeutralsAsHostiles: true);
                if (EnemiesAround.Count > 0)
                {
                    IsFleeing = true;
                    Flee(EnemiesAround);
                }
            }
        }

        private void Flee(List<Ingame.MyDetectedEntityInfo> RadarData = null)
        {
            try
            {
                if (!IsFleeing) return;
                
                try
                {
                    if (!FleeTimersTriggered) TriggerFleeTimers();

                    try
                    {
                        if (RadarData == null) RadarData = LookForEnemies(FreighterSetup.FleeTriggerDistance);
                        if (RadarData.Count == 0) return;

                        try
                        {
                            Ingame.MyDetectedEntityInfo ClosestEnemy = RadarData.OrderBy(x => GridPosition.DistanceTo(x.Position)).FirstOrDefault();

                            if (ClosestEnemy.IsEmpty())
                            {
                                Grid.DebugWrite("Flee", "Cannot find closest hostile");
                                return;
                            }

                            try
                            {
                                IMyEntity EnemyEntity = MyAPIGateway.Entities.GetEntityById(ClosestEnemy.EntityId);
                                if (EnemyEntity == null)
                                {
                                    Grid.DebugWrite("Flee", "Cannot find enemy entity from closest hostile ID");
                                    return;
                                }

                                try
                                {
                                    //Grid.DebugWrite("Flee", $"Fleeing from '{EnemyEntity.DisplayName}'. Distance: {Math.Round(GridPosition.DistanceTo(ClosestEnemy.Position))}m; FleeTriggerDistance: {FreighterSetup.FleeTriggerDistance}");

                                    Vector3D FleePoint = GridPosition.InverseVectorTo(ClosestEnemy.Position, 100 * 1000);
                                    RC.AddWaypoint(FleePoint, "Flee Point");
                                    (RC as MyRemoteControl).ChangeFlightMode(Ingame.FlightMode.OneWay);
                                    (RC as MyRemoteControl).SetAutoPilotSpeedLimit(DetermineFleeSpeed());
                                    RC.SetAutoPilotEnabled(true);
                                }
                                catch (Exception Scrap)
                                {
                                    Grid.LogError("Flee.AddWaypoint", Scrap);
                                }
                            }
                            catch (Exception Scrap)
                            {
                                Grid.LogError("Flee.LookForEnemies.GetEntity", Scrap);
                            }
                        }
                        catch (Exception Scrap)
                        {
                            Grid.LogError("Flee.LookForEnemies.Closest", Scrap);
                        }
                    }
                    catch (Exception Scrap)
                    {
                        Grid.LogError("Flee.LookForEnemies", Scrap);
                    }
                }
                catch (Exception Scrap)
                {
                    Grid.LogError("Flee.TriggerTimers", Scrap);
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("Flee", Scrap);
            }
        }

        private void JumpAway()
        {
            List<IMyJumpDrive> JumpDrives = Term.GetBlocksOfType<IMyJumpDrive>(collect: x => x.IsWorking);

            if (JumpDrives.Count > 0) JumpDrives.First().Jump(false);
        }

        private void TriggerFleeTimers()
        {
            if (FleeTimersTriggered) return;

            List<IMyTimerBlock> FleeTimers = new List<IMyTimerBlock>();
            Term.GetBlocksOfType(FleeTimers, x => x.IsFunctional && x.Enabled && (x.CustomName.Contains("Flee") || x.CustomData.Contains("Flee")));
            Grid.DebugWrite("TriggerFleeTimers", $"Flee timers found: {FleeTimers.Count}.{(FleeTimers.Count > 0 ? " Trying to activate..." : "")}");
            foreach (IMyTimerBlock Timer in FleeTimers)
                Timer.Trigger();

            FleeTimersTriggered = true;
        }

        private float DetermineFleeSpeed()
        {
            return Math.Min(FreighterSetup.FleeSpeedCap, FreighterSetup.FleeSpeedRatio * RC.GetSpeedCap());
        }

        protected override bool ParseSetup()
        {
            if (BotBase.ReadBotType(RC) != BotType) return false;

            List<string> CustomData = RC.CustomData.Trim().Replace("\r\n", "\n").Split('\n').ToList();
            foreach (string DataLine in CustomData)
            {
                if (DataLine.Contains("EEM_AI")) continue;
                if (DataLine.Contains("Type")) continue;
                var Data = DataLine.Trim().Split(':');
                Data[1] = Data[1].Trim();
                switch (Data[0].Trim())
                {
                    case "Faction":
                        break;
                    case "FleeOnlyWhenDamaged":
                        if (!bool.TryParse(Data[1], out FreighterSetup.FleeOnlyWhenDamaged))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeOnlyWhenDamaged cannot be parsed");
                            return false;
                        }
                        break;
                    case "FleeTriggerDistance":
                        if (!float.TryParse(Data[1], out FreighterSetup.FleeTriggerDistance))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeTriggerDistance cannot be parsed");
                            return false;
                        }
                        break;
                    case "FleeSpeedRatio":
                        if (!float.TryParse(Data[1], out FreighterSetup.FleeSpeedRatio))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeSpeedRatio cannot be parsed");
                            return false;
                        }
                        break;
                    case "FleeSpeedCap":
                        if (!float.TryParse(Data[1], out FreighterSetup.FleeSpeedCap))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeSpeedCap cannot be parsed");
                            return false;
                        }
                        break;
                    case "CruiseSpeed":
                        if (!float.TryParse(Data[1], out FreighterSetup.CruiseSpeed))
                        {
                            DebugWrite("ParseSetup", "AI setup error: CruiseSpeed cannot be parsed");
                            return false;
                        }
                        break;
                    default:
                        DebugWrite("ParseSetup", $"AI setup error: Cannot parse '{DataLine}'");
                        return false;
                }
            }

            FreighterSetup.Default();
            return true;
        }

        protected override void DebugWrite(string Source, string Message, string DebugPrefix = "FreighterBot.")
        {
            base.DebugWrite(Source, Message, DebugPrefix);
        }
    }
}