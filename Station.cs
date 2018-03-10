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
using Sandbox.ModAPI.Weapons;
using System.Timers;

namespace Cheetah.AI
{
    public sealed class StationBot : BotBase
    {
        static readonly bool DebugMode = true;
        const string SecurityOnTimerPrefix = "[Alert_On]";
        const string SecurityOffTimerPrefix = "[Alert_Off]";
        static public readonly BotTypes BotType = BotTypes.Station;

        bool WasDamaged { get { return AlertTriggerTime != null; } }
        DateTime? AlertTriggerTime = null;
        Timer CalmdownTimer = new Timer();

        public StationBot(IMyCubeGrid Grid) : base(Grid)
        {
        }

        public override bool Init(IMyRemoteControl RC = null)
        {
            if (!base.Init(RC)) return false;
            Update = MyEntityUpdateEnum.EACH_100TH_FRAME;
            Alert += OnAlert;
            OnDamaged += DamageHandler;
            OnBlockPlaced += BlockPlacedHandler;

            CalmdownTimer.AutoReset = false;
            CalmdownTimer.Elapsed += (trash1, trash2) =>
            {
                CalmdownTimer.Stop();
                CalmDown();
            };

            return true;
        }

        private void DamageHandler(IMySlimBlock Block, MyDamageInformation Damage)
        {
            if (Block == null) return;
            if (!Block.IsDestroyed && Damage.IsThruster()) return;
            IMyPlayer Damager;
            ReactOnDamage(Block, Damage, CalmdownTime, out Damager);
            if (Damager != null)
            {
                OnAlert();
            }
        }

        private void OnAlert()
        {
            try
            {
                Grid.DebugWrite("OnAlert", "Alert activated.");
                if (!WasDamaged) Default_SwitchTurretsAndRunTimers(SecurityState: true);
                AlertTriggerTime = DateTime.Now;
                CalmdownTimer.Interval = CalmdownTime.TotalMilliseconds;
                CalmdownTimer.Start();
            }
            catch (Exception Scrap)
            {
                LogError("OnAlert", Scrap, "Station.");
            }
        }

        public override void Main()
        {
            //if (WasDamaged && DateTime.Now - AlertTriggerTime > CalmdownTime) CalmDown();
        }

        void CalmDown()
        {
            try
            {
                Grid.DebugWrite("CalmDown", "Calmdown activated");
                AlertTriggerTime = null;
                Default_SwitchTurretsAndRunTimers(SecurityState: false);
            }
            catch (Exception Scrap)
            {
                Grid.LogError("Calmdown", Scrap);
            }
        }

        protected override bool ParseSetup()
        {
            return true;
        }

        void Default_SwitchTurretsAndRunTimers(bool SecurityState)
        {
            /*try
            {
                List<IMyLargeTurretBase> Turrets = Term.GetBlocksOfType<IMyLargeTurretBase>();
                foreach (IMyLargeTurretBase Turret in Turrets)
                {
                    Turret.SetSecurity_EEM(SecurityState);
                }
            }
            catch (Exception Scrap)
            {
                LogError("SwitchTurrets", Scrap, "StationBot.");
            }*/
            try
            {
                List<IMyTimerBlock> AlertTimers = Term.GetBlocksOfType<IMyTimerBlock>
                    (x => x.IsWorking && x.CustomName.Contains(SecurityState ? SecurityOnTimerPrefix : SecurityOffTimerPrefix)
                    || x.CustomData.Contains(SecurityState ? SecurityOnTimerPrefix : SecurityOffTimerPrefix));

                foreach (IMyTimerBlock Timer in AlertTimers)
                {
                    Timer.Trigger();
                }
            }
            catch (Exception Scrap)
            {
                LogError("TriggerTimers", Scrap, "StationBot.");
            }

            try
            {
                List<IMyRadioAntenna> CallerAntennae = Term.GetBlocksOfType<IMyRadioAntenna>
                    (x => x.IsWorking && x.CustomData.Contains("Security:CallForHelp"));
                foreach (IMyRadioAntenna Antenna in CallerAntennae)
                {
                    Antenna.Enabled = SecurityState;
                }
            }
            catch (Exception Scrap)
            {
                LogError("EnableAntennae", Scrap, "StationBot.");
            }

            try
            {
                if (AISessionCore.Debug) MyAPIGateway.Utilities.ShowMessage($"{Grid.DisplayName}", $"{(SecurityState ? "Security Alert!" : "Security calmdown")}");
            }
            finally { }
        }
    }

    // Unfinished
    /*
    public class FactoryManager
    {
        readonly IMyCubeGrid Grid;
        readonly IMyGridTerminalSystem Term;
        List<IMyTerminalBlock> InventoryOwners = new List<IMyTerminalBlock>();
        List<IMyAssembler> Assemblers = new List<IMyAssembler>();
        Dictionary<MyDefinitionId, float> ItemsTotal = new Dictionary<MyDefinitionId, float>();
        Dictionary<MyDefinitionId, float> ItemMinimalQuotas = new Dictionary<MyDefinitionId, float>();

        public FactoryManager(IMyCubeGrid Grid)
        {
            this.Grid = Grid;
            Term = Grid.GetTerminalSystem();
        }

        public void LoadInventoryOwners()
        {
            InventoryOwners = Grid.GetBlocks<IMyTerminalBlock>(x => x.HasInventory);
        }

        void ParseAssemblerQuotas(string Input)
        {
            var CustomData = Input.Trim().Replace("\r\n", "\n").Split('\n');
            foreach (string DataLine in CustomData)
            {
                // Syntax:
                // MinQuota:Type/Subtype:Amount
                if (DataLine.StartsWith("MinQuota"))
                {
                    var Data = DataLine.Split(':');
                    MyDefinitionId Definition;
                    float Quota;
                    if (MyDefinitionId.TryParse(Data[1], out Definition) && float.TryParse(Data[2], out Quota))
                    {
                        if (!ItemMinimalQuotas.ContainsKey(Definition))
                            ItemMinimalQuotas.Add(Definition, Quota);
                        else
                            ItemMinimalQuotas[Definition] += Quota;
                    }
                }
            }
        }

        public void SumItems()
        {
            ItemsTotal.Clear();
            foreach (IMyTerminalBlock InventoryOwner in InventoryOwners)
            {
                foreach (IMyInventory Inventory in InventoryOwner.GetInventories())
                {
                    foreach (IMyInventoryItem Item in Inventory.GetItems())
                    {
                        var Blueprint = Item.GetBlueprint();
                        if (ItemsTotal.ContainsKey(Blueprint))
                            ItemsTotal[Blueprint] += (float)Item.Amount;
                        else
                            ItemsTotal.Add(Blueprint, (float)Item.Amount);
                    }
                }
            }
        }
    }*/
}