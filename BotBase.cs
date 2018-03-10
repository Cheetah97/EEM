using System;
using System.Collections.Generic;
using System.Linq;
using EEM.HelperClasses;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.SessionComponents;
//using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IMyGridTerminalSystem = Sandbox.ModAPI.IMyGridTerminalSystem;
using IMyRadioAntenna = Sandbox.ModAPI.IMyRadioAntenna;
using IMyRemoteControl = Sandbox.ModAPI.IMyRemoteControl;
using IMyThrust = Sandbox.ModAPI.IMyThrust;
using Ingame = Sandbox.ModAPI.Ingame;
using static EEM.HelperClasses.StaticTools;

namespace EEM
{
    public abstract class BotBase
    {
        protected IMyCubeGrid Grid { get; }

        protected readonly IMyGridTerminalSystem Term;

        protected Vector3D GridPosition => Grid.GetPosition();

        private Vector3D GridVelocity => Grid.Physics.LinearVelocity;

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        internal Vector3D Endpoint { get; set; }

        //public float GridSpeed => (float)GridVelocity.Length();

        private float GridRadius => (float)Grid.WorldVolume.Radius;

        //protected readonly TimeSpan CalmdownTime = (!Constants.GlobalDebugMode ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(3));
        protected readonly TimeSpan CalmdownTime = TimeSpan.FromMinutes(15);

        private bool IsInitialized
        {
            get
            {
                try
                {
                    return Grid != null;
                }
                catch (Exception scrap)
                {
                    LogError("initialized", scrap);
                    return false;
                }
            }
        }

        public bool Initialized => IsInitialized;

        public MyEntityUpdateEnum Update { get; protected set; }

        protected IMyRemoteControl RemoteControl { get; private set; }

        private IMyFaction _ownerFaction;

        protected string DroneNameProvider => $"Drone_{RemoteControl.EntityId}";

        private string DroneName
        {
/*
            get
            {
                return RemoteControl.Name;
            }
*/
            set
            {
                IMyEntity entity = RemoteControl;
                if (!string.IsNullOrEmpty(entity.Name)) return;
                entity.Name = value;
                MyAPIGateway.Entities.SetEntityName(entity);
                //DebugWrite("DroneName_Set", $"Drone EntityName set to {RC.Name}");
            }
        }

        private bool Gridoperable
        {
            get
            {
                try
                {
                    return !Grid.MarkedForClose && !Grid.Closed && Grid.InScene;
                }
                catch (Exception scrap)
                {
                    LogError("gridoperable", scrap);
                    return false;
                }
            }
        }

        private bool _botOperable;

        private bool _closed;

        public bool Operable
        {
            get
            {
                try
                {
                    return !_closed && Initialized && Gridoperable && RemoteControl.IsFunctional && _botOperable;
                }
                catch (Exception scrap)
                {
                    LogError("Operable", scrap);
                    return false;
                }
            }
        }
        
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private static List<IMyRadioAntenna> Antenna { get; set; }

        public delegate void OnDamageTaken(IMySlimBlock damagedBlock, MyDamageInformation damage);

        protected event OnDamageTaken OnDamaged;

        protected delegate void HOnBlockPlaced(IMySlimBlock block);

        protected event HOnBlockPlaced OnBlockPlaced;

        //protected event Action Alert;
        
        protected BotBase(IMyCubeGrid grid)
        {
            if (grid == null) return;
            Grid = grid;
            Term = grid.GetTerminalSystem();
            Antenna = new List<IMyRadioAntenna>();
        }

        public static BotTypes ReadBotType(IMyRemoteControl remoteControl)
        {
            try
            {
                string remoteControlCustomData = remoteControl.CustomData.Trim().Replace("\r\n", "\n");
                List<string> customData = new List<string>(remoteControlCustomData.Split('\n'));

                if (remoteControlCustomData.IsNullEmptyOrWhiteSpace()) return BotTypes.None;
                if (customData.Count < 2)
                {
                    return BotTypes.Invalid;
                    //if (Constants.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception("CustomData consists of less than two lines"));
                    
                }
                if (customData[0].Trim() != "[EEM_AI]")
                {
                    return BotTypes.Invalid;
                    //if (Constants.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception($"AI tag invalid: '{customData[0]}'"));
                }

                string[] bottype = customData[1].Split(':');
                if (bottype[0].Trim() != "Type")
                {
                    return BotTypes.Invalid;
                    //if (Constants.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception($"Type tag invalid: '{bottype[0]}'"));
                }

                BotTypes botType = BotTypes.Invalid;
                switch (bottype[1].Trim())
                {
                    case "Fighter":
                        botType = BotTypes.Fighter;
                        break;
                    case "Freighter":
                        botType = BotTypes.Freighter;
                        break;
                    case "Carrier":
                        botType = BotTypes.Carrier;
                        break;
                    case "Station":
                        botType = BotTypes.Station;
                        break;
                }

                return botType;
            }
            catch (Exception scrap)
            {
                remoteControl.CubeGrid.LogError("[STATIC]BotBase.ReadBotType", scrap);
                return BotTypes.Invalid;
            }
        }

        protected void DebugWrite(string source, string message, string debugPrefix = "BotBase.")
        {
            Grid.DebugWrite(debugPrefix + source, message);
        }

        public virtual bool Init(IMyRemoteControl remoteControl = null)
        {
            RemoteControl = remoteControl ?? Term.GetBlocksOfType<IMyRemoteControl>(collect: x => x.IsFunctional).FirstOrDefault();
            if (remoteControl == null) return false;
            DroneName = DroneNameProvider;

            Antenna = Term.GetBlocksOfType<IMyRadioAntenna>(collect: x => x.IsFunctional);

            bool hasSetup = ParseSetup();
            if (!hasSetup) return false;

            AiSessionCore.AddDamageHandler(Grid, (block, damage) =>
            {
                OnDamaged?.Invoke(block, damage);
            });
            Grid.OnBlockAdded += (block) =>
            {
                OnBlockPlaced?.Invoke(block);
            };
            _ownerFaction = Grid.GetOwnerFaction(RecalculateOwners: true);
            _botOperable = true;
            return true;
        }

/*
        public void RecompilePBs()
        {
            foreach (IMyProgrammableBlock pb in Term.GetBlocksOfType<IMyProgrammableBlock>())
            {
                pb.Recompile();
            }
        }
*/

        protected void ReactOnDamage(IMySlimBlock block, MyDamageInformation damage, TimeSpan truceDelay, out IMyPlayer damager)
        {
            damager = null;
            try
            {
                if (damage.IsDamagedByDeformation())
                {
                    Grid.DebugWrite("ReactOnDamage", "Grid was damaged by deformation. Ignoring.");
                    return;
                }
                if (damage.IsMeteor())
                {
                    Grid.DebugWrite("ReactOnDamage", "Grid was damaged by meteor. Ignoring.");
                    return;
                }

                if (damage.IsThruster())
                {
                    if (block != null && !block.IsDestroyed)
                    {
                        Grid.DebugWrite("ReactOnDamage", "Grid was slighly damaged by thruster. Ignoring.");
                        return;
                    }
                }

                try
                {
                    if (damage.IsDoneByPlayer(out damager) && damager != null)
                    {
                        try
                        {
                            Grid.DebugWrite("ReactOnDamage", $"Grid is damaged by player {damager.DisplayName}. Trying to activate alert.");
                            RegisterHostileAction(damager, truceDelay);
                        }
                        catch (Exception scrap)
                        {
                            Grid.LogError("ReactOnDamage.GetDamagerFaction", scrap);
                        }
                    }
                    else Grid.DebugWrite("ReactOnDamage", "Grid is damaged, but damage source is not recognized as player.");
                }
                catch (Exception scrap)
                {
                    Grid.LogError("ReactOnDamage.IsDamageDoneByPlayer", scrap);
                }
            }
            catch (Exception scrap)
            {
                Grid.LogError("ReactOnDamage", scrap);
            }
        }

        protected void BlockPlacedHandler(IMySlimBlock block)
        {
            if (block == null) return;

            try
            {
                IMyPlayer builder;
                if (!block.IsPlayerBlock(out builder)) return;
                IMyFaction faction = builder.GetFaction();
                if (faction == null) return;
                RegisterHostileAction(faction, CalmdownTime);
            }
            catch (Exception scrap)
            {
                Grid.LogError("BlokPlaedHandler", scrap);
            }
        }

        private void RegisterHostileAction(IMyPlayer player, TimeSpan truceDelay)
        {
            try
            {
                if (player == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: Damager is null.");
                    return;
                }

                if (_ownerFaction == null)
                {
                    _ownerFaction = Grid.GetOwnerFaction();
                }

                if (_ownerFaction == null || !_ownerFaction.IsNPC())
                {
                    Grid.DebugWrite("RegisterHostileAction", $"Error: {(_ownerFaction == null ? "can't find own faction" : "own faction isn't recognized as NPC.")}");
                    return;
                }

                IMyFaction hostileFaction = player.GetFaction();
                if (hostileFaction == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: can't find damager's faction");
                    return;
                }
                else if (hostileFaction == _ownerFaction)
                {
                    _ownerFaction.Kick(player);
                    return;
                }

                AiSessionCore.DeclareWar(_ownerFaction, hostileFaction, truceDelay);
                if (!_ownerFaction.IsLawful()) return;
                AiSessionCore.DeclareWar(Diplomacy.Police, hostileFaction, truceDelay);
                AiSessionCore.DeclareWar(Diplomacy.Army, hostileFaction, truceDelay);
            }
            catch (Exception scrap)
            {
                LogError("RegisterHostileAction", scrap);
            }
        }

        private void RegisterHostileAction(IMyFaction hostileFaction, TimeSpan truceDelay)
        {
            try
            {
                if (hostileFaction == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: can't find damager's faction");
                    return;
                }
                AiSessionCore.DeclareWar(_ownerFaction, hostileFaction, truceDelay);
                if (!_ownerFaction.IsLawful()) return;
                AiSessionCore.DeclareWar(Diplomacy.Police, hostileFaction, truceDelay);
                AiSessionCore.DeclareWar(Diplomacy.Army, hostileFaction, truceDelay);
            }
            catch (Exception scrap)
            {
                LogError("RegisterHostileAction", scrap);
            }
        }

        private List<Ingame.MyDetectedEntityInfo> LookAround(float radius, Func<Ingame.MyDetectedEntityInfo, bool> filter = null)
        {
            List<Ingame.MyDetectedEntityInfo> radarData = new List<Ingame.MyDetectedEntityInfo>();
            BoundingSphereD lookaroundSphere = new BoundingSphereD(GridPosition, radius);

            List<IMyEntity> entitiesAround = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref lookaroundSphere);
            entitiesAround.RemoveAll(x => x == Grid || GridPosition.DistanceTo(x.GetPosition()) < GridRadius * 1.5);

            long ownerId;
            if (_ownerFaction != null)
            {
                ownerId = _ownerFaction.FounderId;
                Grid.DebugWrite("LookAround", "Found owner via faction owner");
            }
            else
            {
                ownerId = RemoteControl.OwnerId;
                Grid.DebugWrite("LookAround", "OWNER FACTION NOT FOUND, found owner via RC owner");
            }

            foreach (IMyEntity detectedEntity in entitiesAround)
            {
                Ingame.MyDetectedEntityInfo radarDetectedEntity = MyDetectedEntityInfoHelper.Create(detectedEntity as MyEntity, ownerId);
                if (radarDetectedEntity.Type == Ingame.MyDetectedEntityType.None || radarDetectedEntity.Type == Ingame.MyDetectedEntityType.Unknown) continue;
                if (filter?.Invoke(radarDetectedEntity) ?? true) radarData.Add(radarDetectedEntity);
            }

            //DebugWrite("LookAround", $"Radar entities detected: {String.Join(" | ", RadarData.Select(x => $"{x.Name}"))}");
            return radarData;
        }

        protected List<Ingame.MyDetectedEntityInfo> LookForEnemies(float radius, bool considerNeutralsAsHostiles = false, Func<Ingame.MyDetectedEntityInfo, bool> filter = null) => 
            !considerNeutralsAsHostiles ? LookAround(radius, x => x.IsHostile() && (filter?.Invoke(x) ?? true)) : LookAround(radius, x => x.IsNonFriendly() && (filter?.Invoke(x) ?? true));

        /// <summary>
        /// Returns distance from the grid to an object.
        /// </summary>
        protected float Distance(Ingame.MyDetectedEntityInfo target)
        {
            return (float)Vector3D.Distance(GridPosition, target.Position);
        }

/*
        /// <summary>
        /// Returns distance from the grid to an object.
        /// </summary>
        protected float Distance(IMyEntity target)
        {
            return (float)Vector3D.Distance(GridPosition, target.GetPosition());
        }
*/

/*
        protected Vector3 RelVelocity(Ingame.MyDetectedEntityInfo target)
        {
            return target.Velocity - GridVelocity;
        }
*/

        protected float RelSpeed(Ingame.MyDetectedEntityInfo target)
        {
            return (float)(target.Velocity - GridVelocity).Length();
        }

/*
        protected Vector3 RelVelocity(IMyEntity target)
        {
            return target.Physics.LinearVelocity - GridVelocity;
        }
*/

/*
        protected float RelSpeed(IMyEntity target)
        {
            return (float)(target.Physics.LinearVelocity - GridVelocity).Length();
        }
*/

/*
        protected List<IMyTerminalBlock> GetHackedBlocks()
        {
            List<IMyTerminalBlock> terminalBlocks = new List<IMyTerminalBlock>();
            List<IMyTerminalBlock> hackedBlocks = new List<IMyTerminalBlock>();

            Term.GetBlocks(terminalBlocks);

            foreach (IMyTerminalBlock block in terminalBlocks)
                if (block.IsBeingHacked) hackedBlocks.Add(block);

            return hackedBlocks;
        }
*/

/*
        protected List<IMySlimBlock> GetDamagedBlocks()
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            Grid.GetBlocks(blocks, x => x.CurrentDamage > 10);
            return blocks;
        }
*/

        private bool HasModdedThrusters => _speedmoddedThrusters.Count > 0;

        private readonly List<IMyThrust> _speedmoddedThrusters = new List<IMyThrust>();
        
        protected void ApplyThrustMultiplier(float thrustMultiplier)
        {
            DemultiplyThrusters();
            //foreach (IMyThrust thruster in Term.GetBlocksOfType<IMyThrust>(collect: x => x.IsOwnedByNPC(AllowNobody: false, CheckBuilder: true)))
            foreach (IMyThrust thruster in Term.GetBlocksOfType<IMyThrust>())
            {
                thruster.ThrustMultiplier = thrustMultiplier;
                //Thruster.OwnershipChanged += Thruster_OnOwnerChanged;
                _speedmoddedThrusters.Add(thruster);
            }
        }

        private void DemultiplyThrusters()
        {
            if (!HasModdedThrusters) return;
            foreach (IMyThrust thruster in _speedmoddedThrusters)
            {
                if (Math.Abs(thruster.ThrustMultiplier - 1) > 0) thruster.ThrustMultiplier = 1;
            }
            _speedmoddedThrusters.Clear();
        }

        //private void Thruster_OnOwnerChanged(IMyTerminalBlock thruster)
        //{
        //    try
        //    {
        //        IMyThrust Thruster = thruster as IMyThrust;
        //        if (Thruster == null) return;
        //        if (!Thruster.IsOwnedByNPC() && Thruster.ThrustMultiplier != 1) Thruster.ThrustMultiplier = 1;
        //    }
        //    catch (Exception Scrap)
        //    {
        //        Grid.DebugWrite("Thruster_OnOwnerChanged", $"{thruster.CustomName} OnOwnerChanged failed: {Scrap.Message}");
        //    }
        //}

        protected abstract bool ParseSetup();

        public abstract void Main();

        public void Shutdown()
        {
            _closed = true;
            if (HasModdedThrusters) DemultiplyThrusters();
            AiSessionCore.RemoveDamageHandler(Grid);
        }

        protected void LogError(string source, Exception scrap, string debugPrefix = "BotBase.")
        {
            Grid.LogError(debugPrefix + source, scrap);
        }
    }

    /*public sealed class InvalidBot : BotBase
    {
        static public readonly BotTypes BotType = BotTypes.None;

        public override bool Operable
        {
            get
            {
                return false;
            }
        }

        public InvalidBot(IMyCubeGrid Grid = null) : base(Grid)
        {
        }

        public override bool Init(IMyRemoteControl RC = null)
        {
            return false;
        }

        public override void Main()
        {
            // Empty
        }

        protected override bool ParseSetup()
        {
            return false;
        }
    }*/
}