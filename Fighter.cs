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
    public sealed class FighterBot : BotBase
    {
        static public readonly BotTypes BotType = BotTypes.Fighter;
        private bool Damaged = false;
        public bool KeenAILoaded { get; private set; }
        FighterSettings FighterSetup;
        struct FighterSettings
        {
            public string Preset;
            public bool CallHelpOnDamage;
            public bool AssignToPirates;
            public bool DelayedAIEnable;
            public bool AmbushMode;
            public bool AttackNeutrals;
            public float SeekDistance;
            public float AIActivationDistance;
            //public int PlayerPriority;
            public int CallHelpProbability;

            /// <summary>
            /// Fills out the empty values in struct with default values, and leaves filled values untouched.
            /// </summary>
            public void Default(bool RandomizeCallHelp = true)
            {
                if (Preset == default(string)) Preset = "DefaultDirect";
                if (AssignToPirates == default(bool)) { }
                if (AmbushMode == default(bool)) { }
                if (DelayedAIEnable == default(bool)) { }
                if (SeekDistance == default(float)) SeekDistance = 10000;
                //if (PlayerPriority == default(int)) PlayerPriority = 10;
                if (CallHelpProbability == default(int)) CallHelpProbability = 100;
                if (CallHelpOnDamage == default(bool) || RandomizeCallHelp) this.RandomizeCallHelp();
            }

            private void RandomizeCallHelp()
            {
                Random Rand = new Random();
                int random = Rand.Next(0, 101);

                if (random <= CallHelpProbability) CallHelpOnDamage = true;
            }

            public override string ToString()
            {
                return $"Preset='{Preset}|CallHelp={CallHelpOnDamage}|AssignToPirates={AssignToPirates}|SeekDistance={SeekDistance}|AttackNeutrals={AttackNeutrals}";
            }
        }

        public FighterBot(IMyCubeGrid Grid) : base(Grid)
        {
        }

        public override bool Init(IMyRemoteControl RC = null)
        {
            if (!base.Init(RC)) return false;
            Update |= MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (FighterSetup.CallHelpOnDamage) OnDamaged += DamageHandler;

            RC.Name = DroneNameProvider;
            MyAPIGateway.Entities.SetEntityName(RC as IMyEntity, true);
            if (!FighterSetup.DelayedAIEnable) LoadKeenAI();
            //DebugWrite("Init", $"Fighter bot successfully initialized. Setup: {FighterSetup}");
            return true;
        }

        public void LoadKeenAI()
        {
            try
            {
                if (KeenAILoaded) return;
                (RC as MyRemoteControl).SetAutoPilotSpeedLimit(RC.GetSpeedCap());
                MyVisualScriptLogicProvider.SetDroneBehaviourFull(RC.Name, presetName: FighterSetup.Preset, maxPlayerDistance: FighterSetup.SeekDistance, playerPriority: 0, assignToPirates: FighterSetup.AssignToPirates);
                if (FighterSetup.AmbushMode == true) MyVisualScriptLogicProvider.DroneSetAmbushMode(RC.Name, ambushModeOn: true);
                MyVisualScriptLogicProvider.TargetingSetWhitelist(RC.Name);
                KeenAILoaded = true;
            }
            catch (Exception Scrap)
            {
                Grid.LogError("LoadKeenAI", Scrap);
            }
        }

        private void DamageHandler(IMySlimBlock Block, MyDamageInformation Damage)
        {
            if (Damaged) return;
            Damaged = true;

            IMyPlayer Damager;
            ReactOnDamage(Block, Damage, CalmdownTime, out Damager);

            if (FighterSetup.DelayedAIEnable) LoadKeenAI();

            foreach (IMyTimerBlock Timer in Term.GetBlocksOfType<IMyTimerBlock>(collect: x => x.IsFunctional && x.Enabled
            && x.CustomName.Contains("Damage")))
                Timer.Trigger();

            if (!FighterSetup.CallHelpOnDamage) return;

            foreach (IMyTimerBlock Timer in Term.GetBlocksOfType<IMyTimerBlock>(collect: x => x.IsFunctional && x.Enabled
            && x.CustomName.Contains("Security")))
                Timer.Trigger();
        }

        public override void Main()
        {
            if (FighterSetup.DelayedAIEnable && !KeenAILoaded) { DelayedAI_Main(); return; }

            List<Ingame.MyDetectedEntityInfo> EnemiesInProximity = LookForEnemies(FighterSetup.SeekDistance, FighterSetup.AttackNeutrals);
            MyVisualScriptLogicProvider.DroneTargetLoseCurrent(RC.Name);
            if (EnemiesInProximity.Count > 0)
                MyVisualScriptLogicProvider.DroneSetTarget(RC.Name, GetTopPriorityTarget(EnemiesInProximity).GetEntity() as MyEntity);
        }

        Ingame.MyDetectedEntityInfo GetTopPriorityTarget(List<Ingame.MyDetectedEntityInfo> Targets)
        {
            if (Targets == null || Targets.Count == 0) return new Ingame.MyDetectedEntityInfo();
            if (Targets.Count == 1) return Targets.First();

            List<Ingame.MyDetectedEntityInfo> MostDangerous = new List<Ingame.MyDetectedEntityInfo>();
            if (Targets.Any(x => Distance(x) <= 200 && RelSpeed(x) <= 40, out MostDangerous))
                return MostDangerous.OrderBy(x => Distance(x)).First();

            List<Ingame.MyDetectedEntityInfo> TargetsClose = Targets.Where(x => Distance(x) <= 1200).ToList();
            if (TargetsClose.Count > 0) return TargetsClose.OrderBy(x => DangerIndex(x) / x.GetMassT()).First();
            List<Ingame.MyDetectedEntityInfo> TargetsFar = Targets.Where(x => Distance(x) > 1200).ToList();
            if (TargetsFar.Count > 0) return TargetsFar.OrderBy(x => DangerIndex(x) / x.GetMassT()).First();
            return new Ingame.MyDetectedEntityInfo();
        }

        float DangerIndex(Ingame.MyDetectedEntityInfo Enemy)
        {
            if (Enemy.Type == Ingame.MyDetectedEntityType.CharacterHuman)
                return Distance(Enemy) < 100 ? 100 : 10;
            if (!Enemy.IsGrid()) return 0;

            float DangerIndex = 0;
            IMyCubeGrid EnemyGrid = Enemy.GetGrid();
            if (MyTrashRemoval.IsTrash(EnemyGrid as MyEntity)) return 0;

            List<IMySlimBlock> EnemySlimBlocks = new List<IMySlimBlock>();
            EnemyGrid.GetBlocks(EnemySlimBlocks, x => x.FatBlock != null && x.FatBlock is IMyTerminalBlock);
            List<IMyTerminalBlock> EnemyBlocks = EnemySlimBlocks.Select(x => x.FatBlock as IMyTerminalBlock).ToList();
            DangerIndex += EnemyBlocks.Count(x => x is IMyLargeMissileTurret) * 300;
            DangerIndex += EnemyBlocks.Count(x => x is IMyLargeGatlingTurret) * 100;
            DangerIndex += EnemyBlocks.Count(x => x is IMySmallMissileLauncher) * 400;
            DangerIndex += EnemyBlocks.Count(x => x is IMySmallGatlingGun) * 250;
            DangerIndex += EnemyBlocks.Count(x => x is IMyLargeInteriorTurret) * 40;

            if (Enemy.Type == Ingame.MyDetectedEntityType.LargeGrid) DangerIndex *= 2.5f;
            return DangerIndex;
        }

        void DelayedAI_Main()
        {
            try
            {
                if (FighterSetup.DelayedAIEnable && !KeenAILoaded && FighterSetup.AIActivationDistance > 0)
                {
                    try
                    {
                        var EnemiesInProximity = LookForEnemies(FighterSetup.AIActivationDistance);
                        if (EnemiesInProximity == null)
                            throw new Exception("WEIRD: EnemiesInProximity == null");
                        else if (EnemiesInProximity.Count > 0) LoadKeenAI();
                    }
                    catch (Exception Scrap)
                    {
                        Grid.LogError("Fighter.DelayedAI.LookForEnemies", Scrap);
                    }
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("Fighter.DelayedAI", Scrap);
            }
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
                Data[0] = Data[0].Trim();
                Data[1] = Data[1].Trim();

                switch (Data[0])
                {
                    case "Faction":
                        break;
                    case "Preset":
                        FighterSetup.Preset = Data[1];
                        break;
                    case "DelayedAI":
                        if (!bool.TryParse(Data[1], out FighterSetup.DelayedAIEnable))
                        {
                            DebugWrite("ParseSetup", "AI setup error: DelayedAI cannot be parsed");
                            return false;
                        }
                        break;
                    case "AssignToPirates":
                        if (!bool.TryParse(Data[1], out FighterSetup.AssignToPirates))
                        {
                            DebugWrite("ParseSetup", "AI setup error: AssignToPirates cannot be parsed");
                            return false;
                        }
                        break;
                    case "AttackNeutrals":
                        if (!bool.TryParse(Data[1], out FighterSetup.AttackNeutrals))
                        {
                            DebugWrite("ParseSetup", "AI setup error: AttackNeutrals cannot be parsed");
                            return false;
                        }
                        break;
                    case "AmbushMode":
                        if (!bool.TryParse(Data[1], out FighterSetup.AmbushMode))
                        {
                            DebugWrite("ParseSetup", "AI setup error: AmbushMode cannot be parsed");
                            return false;
                        }
                        break;
                    case "SeekDistance":
                        if (!float.TryParse(Data[1], out FighterSetup.SeekDistance))
                        {
                            DebugWrite("ParseSetup", "AI setup error: SeekDistance cannot be parsed");
                            return false;
                        }
                        break;
                    case "ActivationDistance":
                        if (!float.TryParse(Data[1], out FighterSetup.AIActivationDistance))
                        {
                            DebugWrite("ParseSetup", "AI setup error: ActivationDistance cannot be parsed");
                            return false;
                        }
                        break;
                    case "PlayerPriority":
                        DebugWrite("ParseSetup", "AI setup warning: PlayerPriority is deprecated and no longer used.");
                        break;
                    case "CallHelpProbability":
                        int probability;
                        if (!int.TryParse(Data[1], out probability))
                        {
                            DebugWrite("ParseSetup", "AI setup error: CallHelpProbability cannot be parsed");
                            return false;
                        }
                        else if (probability < 0 || probability > 100)
                        {
                            DebugWrite("ParseSetup", "AI setup error: CallHelpProbability out of bounds. Must be between 0 and 100");
                            return false;
                        }
                        else
                        {
                            FighterSetup.CallHelpProbability = probability;
                        }
                        break;
                    case "ThrustMultiplier":
                        float multiplier;
                        if (!float.TryParse(Data[1], out multiplier))
                        {
                            DebugWrite("ParseSetup", "AI setup error: ThrustMultiplier cannot be parsed");
                            return false;
                        }
                        else
                        {
                            ApplyThrustMultiplier(multiplier);
                        }
                        break;
                    default:
                        DebugWrite("ParseSetup", $"AI setup error: Cannot parse '{DataLine}'");
                        return false;
                }
            }
            FighterSetup.Default();
            return true;
        }

        protected override void DebugWrite(string Source, string Message, string DebugPrefix = "FighterBot.")
        {
            base.DebugWrite(Source, Message, DebugPrefix);
        }
    }
}