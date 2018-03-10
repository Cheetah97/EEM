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
        public IMyCubeGrid Grid { get; protected set; }

        protected readonly IMyGridTerminalSystem Term;

        protected Vector3D GridPosition => Grid.GetPosition();

        private Vector3D GridVelocity => Grid.Physics.LinearVelocity;

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        internal Vector3D Endpoint { get; set; }

        //public float GridSpeed => (float)GridVelocity.Length();

        private float GridRadius => (float)Grid.WorldVolume.Radius;

        //protected readonly TimeSpan CalmdownTime = (!Constants.GlobalDebugMode ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(3));
        protected readonly TimeSpan PeaceDelay = TimeSpan.FromMinutes(15);

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

        public IMyRemoteControl RemoteControl { get; private set; }

        public IMyFaction OwnerFaction { get; private set; }

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
        
        public BotRadar RadarModule { get; protected set; }

        public BotDamageHandler DamageModule { get; protected set; }

        public BotDiplomacyHandler DiplomacyModule { get; protected set; }
        
        protected BotBase(IMyCubeGrid grid)
        {
            if (grid == null) return;
            Grid = grid;
            Term = grid.GetTerminalSystem();
        }

        //This really shouldn't be here, moved to BotTypeHelper.cs
        //public static BotTypes ReadBotType(IMyRemoteControl remoteControl)

        protected void DebugWrite(string source, string message, string debugPrefix = "BotBase.")
        {
            Grid.DebugWrite(debugPrefix + source, message);
        }

        public virtual bool Init(IMyRemoteControl remoteControl = null)
        {
            RemoteControl = remoteControl ?? Term.GetBlocksOfType<IMyRemoteControl>(collect: x => x.IsFunctional).FirstOrDefault();
            if (remoteControl == null) return false;
            DroneName = DroneNameProvider;

            bool hasSetup = ParseSetup();
            if (!hasSetup) return false;

            OwnerFaction = Grid.GetOwnerFaction(RecalculateOwners: true);
            InitModules();
            _botOperable = true;
            return true;
        }

        /// <summary>
        /// Remember that all three components should be initialized: Radar, Diplomacy, Damage Handling.
        /// </summary>
        protected abstract void InitModules();

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
            DamageModule.Close();
            DiplomacyModule.Close();
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