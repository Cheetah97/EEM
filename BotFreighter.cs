using System;
using System.Collections.Generic;
using System.Linq;
using EEM.HelperClasses;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
//using IMyJumpDrive = Sandbox.ModAPI.IMyJumpDrive;
using IMyRemoteControl = Sandbox.ModAPI.IMyRemoteControl;
using Ingame = Sandbox.ModAPI.Ingame;


namespace EEM
{
    public sealed class BotFreighter : BotBase
    {
        private static readonly BotTypes BotType = BotTypes.Freighter;
        private FreighterSettings _freighterSetup;

        private struct FreighterSettings
        {
            public bool FleeOnlyWhenDamaged;
            public float FleeTriggerDistance;
            public float FleeSpeedRatio;
            public float FleeSpeedCap;
            public float CruiseSpeed;

            public void Default()
            {
                if (FleeOnlyWhenDamaged == default(bool)) FleeOnlyWhenDamaged = false;
                if (Math.Abs(FleeTriggerDistance - default(float)) < 0) FleeTriggerDistance = 1000;
                if (Math.Abs(FleeSpeedRatio - default(float)) < 0) FleeSpeedRatio = 1.0f;
                if (Math.Abs(FleeSpeedCap - default(float)) < 0) FleeSpeedCap = 300;
            }
        }
        private bool _isFleeing;
        private bool _fleeTimersTriggered;
        
        public BotFreighter(IMyCubeGrid grid) : base(grid)
        {
        }

        public override bool Init(IMyRemoteControl remoteControl = null)
        {
            if (!base.Init(remoteControl)) return false;
            OnDamaged += DamageHandler;
            OnBlockPlaced += BlockPlacedHandler;

            SetFlightPath();

            Update |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            return true;
        }

        private void SetFlightPath()
        {
            Vector3D velocity = RemoteControl.GetShipVelocities().LinearVelocity;
            float speed = (float)RemoteControl.GetShipSpeed();
            
            if (speed > 5)
            {
                Endpoint = GridPosition + (Vector3D.Normalize(velocity) * 1000000);
            }
            else
            {
                Endpoint = GridPosition + (RemoteControl.WorldMatrix.Forward * 1000000);
            }

            if (Math.Abs(_freighterSetup.CruiseSpeed - default(float)) > 0)
                (RemoteControl as MyRemoteControl)?.SetAutoPilotSpeedLimit(_freighterSetup.CruiseSpeed);
            else
                (RemoteControl as MyRemoteControl)?.SetAutoPilotSpeedLimit(speed > 5 ? speed : 30);

            (RemoteControl as MyRemoteControl)?.SetCollisionAvoidance(true);
        }

        private void DamageHandler(IMySlimBlock block, MyDamageInformation damage)
        {
            try
            {
                IMyPlayer damager;
                ReactOnDamage(block, damage, CalmdownTime, out damager);
                if (damager == null) return;
                _isFleeing = true;
                Flee();
            }
            catch (Exception scrap)
            {
                Grid.LogError("DamageHandler", scrap);
            }
        }

        public override void Main()
        {
            if (_isFleeing) Flee();
            else if (!_freighterSetup.FleeOnlyWhenDamaged)
            {
                List<Ingame.MyDetectedEntityInfo> enemiesAround = LookForEnemies(_freighterSetup.FleeTriggerDistance, considerNeutralsAsHostiles: true);
                if (enemiesAround.Count <= 0) return;
                _isFleeing = true;
                Flee(enemiesAround);
            }
        }

        private void Flee(List<Ingame.MyDetectedEntityInfo> radarData = null)
        {
            try
            {
                if (!_isFleeing) return;
                
                try
                {
                    if (!_fleeTimersTriggered) TriggerFleeTimers();

                    try
                    {
                        if (radarData == null) radarData = LookForEnemies(_freighterSetup.FleeTriggerDistance);
                        if (radarData.Count == 0) return;

                        try
                        {
                            Ingame.MyDetectedEntityInfo closestEnemy = radarData.OrderBy(x => GridPosition.DistanceTo(x.Position)).FirstOrDefault();

                            if (closestEnemy.IsEmpty())
                            {
                                Grid.DebugWrite("Flee", "Cannot find closest hostile");
                                return;
                            }

                            try
                            {
                                IMyEntity enemyEntity = MyAPIGateway.Entities.GetEntityById(closestEnemy.EntityId);
                                if (enemyEntity == null)
                                {
                                    Grid.DebugWrite("Flee", "Cannot find enemy entity from closest hostile ID");
                                    return;
                                }

                                try
                                {
                                    //Grid.DebugWrite("Flee", $"Fleeing from '{EnemyEntity.DisplayName}'. Distance: {Math.Round(GridPosition.DistanceTo(ClosestEnemy.Position))}m; FleeTriggerDistance: {FreighterSetup.FleeTriggerDistance}");

                                    Vector3D fleePoint = GridPosition.InverseVectorTo(closestEnemy.Position, 100 * 1000);
                                    RemoteControl.AddWaypoint(fleePoint, "Flee Point");
                                    (RemoteControl as MyRemoteControl)?.ChangeFlightMode(Ingame.FlightMode.OneWay);
                                    (RemoteControl as MyRemoteControl)?.SetAutoPilotSpeedLimit(DetermineFleeSpeed());
                                    RemoteControl.SetAutoPilotEnabled(true);
                                }
                                catch (Exception scrap)
                                {
                                    Grid.LogError("Flee.AddWaypoint", scrap);
                                }
                            }
                            catch (Exception scrap)
                            {
                                Grid.LogError("Flee.LookForEnemies.GetEntity", scrap);
                            }
                        }
                        catch (Exception scrap)
                        {
                            Grid.LogError("Flee.LookForEnemies.Closest", scrap);
                        }
                    }
                    catch (Exception scrap)
                    {
                        Grid.LogError("Flee.LookForEnemies", scrap);
                    }
                }
                catch (Exception scrap)
                {
                    Grid.LogError("Flee.TriggerTimers", scrap);
                }
            }
            catch (Exception scrap)
            {
                Grid.LogError("Flee", scrap);
            }
        }

/*
        private void JumpAway()
        {
            List<IMyJumpDrive> jumpDrives = Term.GetBlocksOfType<IMyJumpDrive>(collect: x => x.IsWorking);

            if (jumpDrives.Count > 0) jumpDrives.First().Jump(false);
        }
*/
        private void TriggerFleeTimers()
        {
            if (_fleeTimersTriggered) return;

            List<IMyTimerBlock> fleeTimers = new List<IMyTimerBlock>();
            Term.GetBlocksOfType(fleeTimers, x => x.IsFunctional && x.Enabled && (x.CustomName.Contains("Flee") || x.CustomData.Contains("Flee")));
            Grid.DebugWrite("TriggerFleeTimers", $"Flee timers found: {fleeTimers.Count}.{(fleeTimers.Count > 0 ? " Trying to activate..." : "")}");
            foreach (IMyTimerBlock timer in fleeTimers)
                timer.Trigger();

            _fleeTimersTriggered = true;
        }

        private float DetermineFleeSpeed()
        {
            return Math.Min(_freighterSetup.FleeSpeedCap, _freighterSetup.FleeSpeedRatio * RemoteControl.GetSpeedCap());
        }

        protected override bool ParseSetup()
        {
            if (ReadBotType(RemoteControl) != BotType) return false;

            List<string> customData = RemoteControl.CustomData.Trim().Replace("\r\n", "\n").Split('\n').ToList();
            foreach (string dataLine in customData)
            {
                if (dataLine.Contains("EEM_AI")) continue;
                if (dataLine.Contains("Type")) continue;
                string[] data = dataLine.Trim().Split(':');
                data[1] = data[1].Trim();
                switch (data[0].Trim())
                {
                    case "Faction":
                        break;
                    case "FleeOnlyWhenDamaged":
                        if (!bool.TryParse(data[1], out _freighterSetup.FleeOnlyWhenDamaged))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeOnlyWhenDamaged cannot be parsed");
                            return false;
                        }
                        break;
                    case "FleeTriggerDistance":
                        if (!float.TryParse(data[1], out _freighterSetup.FleeTriggerDistance))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeTriggerDistance cannot be parsed");
                            return false;
                        }
                        break;
                    case "FleeSpeedRatio":
                        if (!float.TryParse(data[1], out _freighterSetup.FleeSpeedRatio))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeSpeedRatio cannot be parsed");
                            return false;
                        }
                        break;
                    case "FleeSpeedCap":
                        if (!float.TryParse(data[1], out _freighterSetup.FleeSpeedCap))
                        {
                            DebugWrite("ParseSetup", "AI setup error: FleeSpeedCap cannot be parsed");
                            return false;
                        }
                        break;
                    case "CruiseSpeed":
                        if (!float.TryParse(data[1], out _freighterSetup.CruiseSpeed))
                        {
                            DebugWrite("ParseSetup", "AI setup error: CruiseSpeed cannot be parsed");
                            return false;
                        }
                        break;
                    default:
                        DebugWrite("ParseSetup", $"AI setup error: Cannot parse '{dataLine}'");
                        return false;
                }
            }

            _freighterSetup.Default();
            return true;
        }

        //protected override void DebugWrite(string source, string message, string debugPrefix = "FreighterBot.")
        //{
        //    base.DebugWrite(source, message, debugPrefix);
        //}
    }
}