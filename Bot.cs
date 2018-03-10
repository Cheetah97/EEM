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
    public enum BotTypes
    {
        None,
        Invalid,
        Station,
        Fighter,
        Freighter,
        Carrier
    }

    static public class BotFabric
    {
        static public BotBase FabricateBot(IMyCubeGrid Grid, IMyRemoteControl RC)
        {
            try
            {
                BotTypes BotType = BotBase.ReadBotType(RC);

                BotBase Bot = null;
                switch (BotType)
                {
                    case BotTypes.Fighter:
                        Bot = new FighterBot(Grid);
                        break;
                    case BotTypes.Freighter:
                        Bot = new FreighterBot(Grid);
                        break;
                    case BotTypes.Station:
                        Bot = new StationBot(Grid);
                        break;
                    default:
                        if (AISessionCore.AllowThrowingErrors) throw new Exception("Invalid bot type");
                        break;
                }

                return Bot;
            }
            catch (Exception Scrap)
            {
                Grid.LogError("BotFabric.FabricateBot", Scrap);
                return null;
            }
        }
    }

    abstract public class BotBase
    {
        public IMyCubeGrid Grid { get; protected set; }
        protected readonly IMyGridTerminalSystem Term;
        public Vector3D GridPosition => Grid.GetPosition();
        public Vector3D GridVelocity => Grid.Physics.LinearVelocity;
        public float GridSpeed => (float)GridVelocity.Length();
        protected float GridRadius => (float)Grid.WorldVolume.Radius;
        protected readonly TimeSpan CalmdownTime = (!AISessionCore.Debug ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(3));
        protected bool initialized
        {
            get
            {
                try
                {
                    return Grid != null;
                }
                catch (Exception Scrap)
                {
                    LogError("initialized", Scrap);
                    return false;
                }
            }
        }
        virtual public bool Initialized => initialized;
        public MyEntityUpdateEnum Update { get; protected set; }
        public IMyRemoteControl RC { get; protected set; }
        IMyFaction OwnerFaction;

        protected string DroneNameProvider => $"Drone_{RC.EntityId}";
        public string DroneName
        {
            get
            {
                return RC.Name;
            }
            protected set
            {
                IMyEntity entity = RC as IMyEntity;
                entity.Name = value;
                MyAPIGateway.Entities.SetEntityName(entity, true);
                //DebugWrite("DroneName_Set", $"Drone EntityName set to {RC.Name}");
            }
        }
        protected bool gridoperable
        {
            get
            {
                try
                {
                    return !Grid.MarkedForClose && !Grid.Closed && Grid.InScene;
                }
                catch (Exception Scrap)
                {
                    LogError("gridoperable", Scrap);
                    return false;
                }
            }
        }
        protected bool BotOperable = false;
        protected bool Closed;
        virtual public bool Operable
        {
            get
            {
                try
                {
                    return !Closed && Initialized && gridoperable && RC.IsFunctional && BotOperable;
                }
                catch (Exception Scrap)
                {
                    LogError("Operable", Scrap);
                    return false;
                }
            }
        }
        public List<IMyRadioAntenna> Antennae { get; protected set; }
        public delegate void OnDamageTaken(IMySlimBlock DamagedBlock, MyDamageInformation Damage);
        protected event OnDamageTaken OnDamaged;
        public delegate void hOnBlockPlaced(IMySlimBlock Block);
        protected event hOnBlockPlaced OnBlockPlaced;
        protected event Action Alert;

        public BotBase(IMyCubeGrid Grid)
        {
            if (Grid == null) return;
            this.Grid = Grid;
            Term = Grid.GetTerminalSystem();
            Antennae = new List<IMyRadioAntenna>();
        }

        static public BotTypes ReadBotType(IMyRemoteControl RC)
        {
            try
            {
                string _CustomData = RC.CustomData.Trim().Replace("\r\n", "\n");
                List<string> CustomData = new List<string>(_CustomData.Split('\n'));

                if (_CustomData.IsNullEmptyOrWhiteSpace()) return BotTypes.None;
                if (CustomData.Count < 2)
                {
                    if (AISessionCore.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception("CustomData consists of less than two lines"));
                    else return BotTypes.Invalid;
                }
                if (CustomData[0].Trim() != "[EEM_AI]")
                {
                    if (AISessionCore.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception($"AI tag invalid: '{CustomData[0]}'"));
                    return BotTypes.Invalid;
                }

                string[] bottype = CustomData[1].Split(':');
                if (bottype[0].Trim() != "Type")
                {
                    if (AISessionCore.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception($"Type tag invalid: '{bottype[0]}'"));
                    return BotTypes.Invalid;
                }

                BotTypes BotType = BotTypes.Invalid;
                switch (bottype[1].Trim())
                {
                    case "Fighter":
                        BotType = BotTypes.Fighter;
                        break;
                    case "Freighter":
                        BotType = BotTypes.Freighter;
                        break;
                    case "Carrier":
                        BotType = BotTypes.Carrier;
                        break;
                    case "Station":
                        BotType = BotTypes.Station;
                        break;
                }

                return BotType;
            }
            catch (Exception Scrap)
            {
                RC.CubeGrid.LogError("[STATIC]BotBase.ReadBotType", Scrap);
                return BotTypes.Invalid;
            }
        }

        virtual protected void DebugWrite(string Source, string Message, string DebugPrefix = "BotBase.")
        {
            Grid.DebugWrite(DebugPrefix + Source, Message);
        }

        virtual public bool Init(IMyRemoteControl RC = null)
        {
            this.RC = RC ?? Term.GetBlocksOfType<IMyRemoteControl>(collect: x => x.IsFunctional).FirstOrDefault();
            if (RC == null) return false;
            DroneName = DroneNameProvider;

            Antennae = Term.GetBlocksOfType<IMyRadioAntenna>(collect: x => x.IsFunctional);

            bool HasSetup = ParseSetup();
            if (!HasSetup) return false;

            AISessionCore.AddDamageHandler(Grid, (Block, Damage) => OnDamaged(Block, Damage));
            Grid.OnBlockAdded += (Block) => OnBlockPlaced(Block);
            OwnerFaction = Grid.GetOwnerFaction(RecalculateOwners: true);
            BotOperable = true;
            return true;
        }

        virtual public void RecompilePBs()
        {
            foreach (IMyProgrammableBlock PB in Term.GetBlocksOfType<IMyProgrammableBlock>())
            {
                PB.Recompile();
            }
        }

        virtual protected void ReactOnDamage(IMySlimBlock Block, MyDamageInformation Damage, TimeSpan TruceDelay, out IMyPlayer Damager)
        {
            Damager = null;
            try
            {
                if (Damage.IsMeteor())
                {
                    Grid.DebugWrite("ReactOnDamage", "Grid was damaged by meteor. Ignoring.");
                    return;
                }

                if (Damage.IsThruster())
                {
                    if (Block != null && !Block.IsDestroyed)
                    {
                        Grid.DebugWrite("ReactOnDamage", "Grid was slighly damaged by thruster. Ignoring.");
                        return;
                    }
                }

                try
                {
                    if (Damage.IsDoneByPlayer(out Damager) && Damager != null)
                    {
                        try
                        {
                            Grid.DebugWrite("ReactOnDamage", $"Grid is damaged by player {Damager.DisplayName}. Trying to activate alert.");
                            RegisterHostileAction(Damager, TruceDelay);
                        }
                        catch (Exception Scrap)
                        {
                            Grid.LogError("ReactOnDamage.GetDamagerFaction", Scrap);
                        }
                    }
                    else Grid.DebugWrite("ReactOnDamage", "Grid is damaged, but damage source is not recognized as player.");
                }
                catch (Exception Scrap)
                {
                    Grid.LogError("ReactOnDamage.IsDamageDoneByPlayer", Scrap);
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("ReactOnDamage", Scrap);
            }
        }

        virtual protected void BlockPlacedHandler(IMySlimBlock Block)
        {
            if (Block == null) return;

            try
            {
                IMyPlayer Builder = null;
                IMyFaction Faction = null;
                if (Block.IsPlayerBlock(out Builder))
                {
                    Faction = Builder.GetFaction();
                    if (Faction != null)
                    {
                        RegisterHostileAction(Faction, CalmdownTime);
                    }
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("BlokPlaedHandler", Scrap);
            }
        }

        virtual protected void RegisterHostileAction(IMyPlayer Player, TimeSpan TruceDelay)
        {
            try
            {
                #region Sanity checks
                if (Player == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: Damager is null.");
                    return;
                };

                if (OwnerFaction == null)
                {
                    OwnerFaction = Grid.GetOwnerFaction();
                }

                if (OwnerFaction == null || !OwnerFaction.IsNPC())
                {
                    Grid.DebugWrite("RegisterHostileAction", $"Error: {(OwnerFaction == null ? "can't find own faction" : "own faction isn't recognized as NPC.")}");
                    return;
                }
                #endregion

                IMyFaction HostileFaction = Player.GetFaction();
                if (HostileFaction == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: can't find damager's faction");
                    return;
                }
                else if (HostileFaction == OwnerFaction)
                {
                    OwnerFaction.Kick(Player);
                    return;
                }

                AISessionCore.DeclareWar(OwnerFaction, HostileFaction, TruceDelay);
                if (OwnerFaction.IsLawful())
                {
                    AISessionCore.DeclareWar(Diplomacy.Police, HostileFaction, TruceDelay);
                    AISessionCore.DeclareWar(Diplomacy.Army, HostileFaction, TruceDelay);
                }
            }
            catch (Exception Scrap)
            {
                LogError("RegisterHostileAction", Scrap);
            }
        }

        virtual protected void RegisterHostileAction(IMyFaction HostileFaction, TimeSpan TruceDelay)
        {
            try
            {
                if (HostileFaction == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: can't find damager's faction");
                    return;
                }
                AISessionCore.DeclareWar(OwnerFaction, HostileFaction, TruceDelay);
                if (OwnerFaction.IsLawful())
                {
                    AISessionCore.DeclareWar(Diplomacy.Police, HostileFaction, TruceDelay);
                    AISessionCore.DeclareWar(Diplomacy.Army, HostileFaction, TruceDelay);
                }
            }
            catch (Exception Scrap)
            {
                LogError("RegisterHostileAction", Scrap);
            }
        }

        protected List<Ingame.MyDetectedEntityInfo> LookAround(float Radius, Func<Ingame.MyDetectedEntityInfo, bool> Filter = null)
        {
            List<Ingame.MyDetectedEntityInfo> RadarData = new List<Ingame.MyDetectedEntityInfo>();
            BoundingSphereD LookaroundSphere = new BoundingSphereD(GridPosition, Radius);

            List<IMyEntity> EntitiesAround = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref LookaroundSphere);
            EntitiesAround.RemoveAll(x => x == Grid || GridPosition.DistanceTo(x.GetPosition()) < GridRadius * 1.5);

            long OwnerID;
            if (OwnerFaction != null)
            {
                OwnerID = OwnerFaction.FounderId;
                Grid.DebugWrite("LookAround", "Found owner via faction owner");
            }
            else
            {
                OwnerID = RC.OwnerId;
                Grid.DebugWrite("LookAround", "OWNER FACTION NOT FOUND, found owner via RC owner");
            }

            foreach (IMyEntity DetectedEntity in EntitiesAround)
            {
                Ingame.MyDetectedEntityInfo RadarDetectedEntity = MyDetectedEntityInfoHelper.Create(DetectedEntity as MyEntity, OwnerID);
                if (RadarDetectedEntity.Type == Ingame.MyDetectedEntityType.None || RadarDetectedEntity.Type == Ingame.MyDetectedEntityType.Unknown) continue;
                if (Filter == null ? true : Filter(RadarDetectedEntity)) RadarData.Add(RadarDetectedEntity);
            }

            //DebugWrite("LookAround", $"Radar entities detected: {String.Join(" | ", RadarData.Select(x => $"{x.Name}"))}");
            return RadarData;
        }

        protected List<Ingame.MyDetectedEntityInfo> LookForEnemies(float Radius, bool ConsiderNeutralsAsHostiles = false, Func<Ingame.MyDetectedEntityInfo, bool> Filter = null)
        {
            if (!ConsiderNeutralsAsHostiles)
                return LookAround(Radius, x => x.IsHostile() && (Filter == null ? true : Filter(x)));
            else
                return LookAround(Radius, x => x.IsNonFriendly() && (Filter == null ? true : Filter(x)));
        }

        /// <summary>
        /// Returns distance from the grid to an object.
        /// </summary>
        protected float Distance(Ingame.MyDetectedEntityInfo Target)
        {
            return (float)Vector3D.Distance(GridPosition, Target.Position);
        }

        /// <summary>
        /// Returns distance from the grid to an object.
        /// </summary>
        protected float Distance(IMyEntity Target)
        {
            return (float)Vector3D.Distance(GridPosition, Target.GetPosition());
        }

        protected Vector3 RelVelocity(Ingame.MyDetectedEntityInfo Target)
        {
            return Target.Velocity - GridVelocity;
        }

        protected float RelSpeed(Ingame.MyDetectedEntityInfo Target)
        {
            return (float)(Target.Velocity - GridVelocity).Length();
        }

        protected Vector3 RelVelocity(IMyEntity Target)
        {
            return Target.Physics.LinearVelocity - GridVelocity;
        }

        protected float RelSpeed(IMyEntity Target)
        {
            return (float)(Target.Physics.LinearVelocity - GridVelocity).Length();
        }

        virtual protected List<IMyTerminalBlock> GetHackedBlocks()
        {
            List<IMyTerminalBlock> TerminalBlocks = new List<IMyTerminalBlock>();
            List<IMyTerminalBlock> HackedBlocks = new List<IMyTerminalBlock>();

            Term.GetBlocks(TerminalBlocks);

            foreach (IMyTerminalBlock Block in TerminalBlocks)
                if (Block.IsBeingHacked) HackedBlocks.Add(Block);

            return HackedBlocks;
        }

        virtual protected List<IMySlimBlock> GetDamagedBlocks()
        {
            List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
            Grid.GetBlocks(Blocks, x => x.CurrentDamage > 10);
            return Blocks;
        }

        protected bool HasModdedThrusters => SpeedmoddedThrusters.Count > 0;
        protected List<IMyThrust> SpeedmoddedThrusters = new List<IMyThrust>();

        protected void ApplyThrustMultiplier(float ThrustMultiplier)
        {
            DemultiplyThrusters();
            foreach (IMyThrust Thruster in Term.GetBlocksOfType<IMyThrust>(collect: x => x.IsOwnedByNPC(AllowNobody: false, CheckBuilder: true)))
            {
                Thruster.ThrustMultiplier = ThrustMultiplier;
                Thruster.OwnershipChanged += Thruster_OnOwnerChanged;
                SpeedmoddedThrusters.Add(Thruster);
            }
        }

        protected void DemultiplyThrusters()
        {
            if (!HasModdedThrusters) return;
            foreach (IMyThrust Thruster in SpeedmoddedThrusters)
            {
                if (Thruster.ThrustMultiplier != 1) Thruster.ThrustMultiplier = 1;
            }
            SpeedmoddedThrusters.Clear();
        }

        private void Thruster_OnOwnerChanged(IMyTerminalBlock thruster)
        {
            try
            {
                IMyThrust Thruster = thruster as IMyThrust;
                if (Thruster == null) return;
                if (!Thruster.IsOwnedByNPC() && Thruster.ThrustMultiplier != 1) Thruster.ThrustMultiplier = 1;
            }
            catch (Exception Scrap)
            {
                Grid.DebugWrite("Thruster_OnOwnerChanged", $"{thruster.CustomName} OnOwnerChanged failed: {Scrap.Message}");
            }
        }

        abstract protected bool ParseSetup();

        abstract public void Main();

        virtual public void Shutdown()
        {
            Closed = true;
            if (HasModdedThrusters) DemultiplyThrusters();
            AISessionCore.RemoveDamageHandler(Grid);
        }

        public void LogError(string Source, Exception Scrap, string DebugPrefix = "BotBase.")
        {
            Grid.LogError(DebugPrefix + Source, Scrap);
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