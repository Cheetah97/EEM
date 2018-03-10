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

namespace Cheetah.AI
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), true)]
    public class GridComponent : MyGameLogicComponent
    {
        private IMyRemoteControl RC;
        public IMyCubeGrid Grid { get; private set; }
        private BotBase AI;

        private bool IsOperable = false;
        public bool CanOperate
        {
            get
            {
                try
                {
                    if (AI == null)
                    {
                        //DebugWrite("CanOperate", "AI is null");
                        return false;
                    }
                    return IsOperable && Grid.InScene && AI.Operable;
                }
                catch (Exception Scrap)
                {
                    //DebugWrite("CanOperate", $"Grid {(Grid != null ? "!=" : "==")} null; AI {(AI != null ? "!=" : "==")} null");
                    LogError("CanOperate", Scrap);
                    return false;
                }
            }
        }

        /// <summary>
        /// Provides a simple way to recompile all PBs on the grid, with given delay.
        /// </summary>
        System.Timers.Timer RecompileDelay = new System.Timers.Timer(500);
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _builder = objectBuilder;
            RC = Entity as IMyRemoteControl;
            Grid = RC.CubeGrid.GetTopMostParent() as IMyCubeGrid;
            //MyAPIGateway.Utilities.ShowMessage($"{Grid.DisplayName}", $"RC component inited");
            RC.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        private void SetupRecompileTimer()
        {
            RecompileDelay.AutoReset = false;
            RecompileDelay.Elapsed += (trash1, trash2) =>
            {
                RecompileDelay.Stop();
                RecompilePBs();
            };
        }

        private void RecompilePBs()
        {
            foreach (IMyProgrammableBlock PB in Grid.GetTerminalSystem().GetBlocksOfType<IMyProgrammableBlock>())
            {
                PB.Recompile();
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!Inited) InitAI();
        }

        // The grid component's updating is governed by AI.Update, and the functions are called as flagged in update.
        public override void UpdateBeforeSimulation() { if (CanOperate) Run(); }
        public override void UpdateBeforeSimulation10() { if (CanOperate) Run(); }
        public override void UpdateBeforeSimulation100() { if (CanOperate) Run(); }

        private bool Inited = false;
        public void InitAI()
        {
            if (!AISessionCore.IsServer) return;
            SetupRecompileTimer();

            try
            {
                if (Grid.Physics == null || (Grid as MyEntity).IsPreview) return;
            }
            catch (Exception Scrap)
            {
                LogError("InitAI[grid check]", Scrap);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(RC.CustomData) || !RC.CustomData.Contains("[EEM_AI]"))
                {
                    Shutdown(Notify: false);
                    return;
                }

                //DebugWrite("GridComponent.InitAI", "Booting up RC component.");

                if (!RC.IsOwnedByNPC())
                {
                    DebugWrite("GridComponent.InitAI", "RC is not owned by NPC!");
                    Shutdown(Notify: false);
                    return;
                }

                if (RC.CustomData.Contains("Faction:"))
                {
                    try
                    {
                        TryAssignToFaction();
                    }
                    catch(Exception Scrap)
                    {
                        LogError("TryAssignToFaction", Scrap);
                    }
                }

                if (RC.CustomData.Contains("Type:None"))
                {
                    IsOperable = true;
                    Inited = true;
                    DebugWrite("GridComponent.InitAI", "Type:None, shutting down.");
                    Shutdown(Notify: false);
                    return;
                }

                BotTypes BotType = BotBase.ReadBotType(RC);
                if (BotType == BotTypes.None)
                {
                    DebugWrite("GridComponent.InitAI", "Skipping grid — no setup found");
                }
                else if (BotType == BotTypes.Invalid)
                {
                    LogError("GridComponent.InitAI", new Exception("Bot type is not valid!", new Exception()));
                }

                //DebugWrite("GridComponent.InitAI", $"Bot found. Bot type: {BotType.ToString()}");
            }
            catch (Exception Scrap)
            {
                LogError("GridComponent.InitAI", Scrap);
                return;
            }

            try
            {
                AI = BotFabric.FabricateBot(Grid, RC);

                if (AI == null)
                {
                    DebugWrite("GridComponent.InitAI", "Bot Fabricator yielded null");
                    Shutdown();
                    return;
                }

                bool init = AI.Init(RC);

                if (init)
                {
                    //DebugWrite("GridComponent.InitAI", "AI.Init() successfully initialized AI component");
                }
                else
                {
                    DebugWrite("GridComponent.InitAI", "AI.Init() returned false — bot initialization failed somewhy");
                    Shutdown();
                    return;
                }

                //DebugWrite("GridComponent.InitAI", $"AI Operable: {AI.Operable}");

                if (AI.Update != default(MyEntityUpdateEnum)) RC.NeedsUpdate |= AI.Update;
                RC.OnMarkForClose += (trash) => { Shutdown(); };
                IsOperable = true;
                Inited = true;
                //Grid.DebugWrite("InitAI", "Grid Component successfully inited.");
            }
            catch (Exception Scrap)
            {
                LogError("GridComponent.InitAI", Scrap);
                Shutdown();
            }
        }

        void TryAssignToFaction(bool RecompilePBs = true)
        {
            try
            {
                if (!AISessionCore.IsServer) return;
                if (string.IsNullOrWhiteSpace(RC.CustomData)) return;

                string CustomData = RC.CustomData.Replace("\r\n", "\n");
                if (!RC.CustomData.Contains("Faction:")) return;

                var split = CustomData.Split('\n').Where(x => x.Contains("Faction:")).ToList();
                if (split.Count() == 0) return;
                string factionLine = split[0].Trim();
                string[] lineSplit = factionLine.Split(':');
                if (lineSplit.Count() != 2)
                {
                    Grid.LogError("TryAssignToFaction", new Exception($"Cannot assign to faction", new Exception($"Line '{factionLine}' cannot be parsed.")));
                    return;
                }
                string factionTag = lineSplit[1].Trim();

                if (factionTag == "Nobody")
                {
                    Grid.ChangeOwnershipSmart(0, MyOwnershipShareModeEnum.All);
                    RecompileDelay.Start();
                    //DebugWrite("TryAssignToFaction", $"Assigned to nobody, recompiled scripts");
                }
                else
                {
                    IMyFaction Faction = MyAPIGateway.Session.Factions.Factions.Values.FirstOrDefault(x => x.Tag == factionTag);

                    if (Faction == null)
                    {
                        Grid.LogError("TryAssignToFaction", new Exception($"Faction with tag '{factionTag}' was not found!"));
                        return;
                    }

                    try
                    {
                        Grid.ChangeOwnershipSmart(Faction.FounderId, MyOwnershipShareModeEnum.Faction);
                        RecompileDelay.Start();
                        //DebugWrite("TryAssignToFaction", $"Assigned to faction '{Data[1]}', recompiled scripts");
                    }
                    catch (Exception Scrap)
                    {
                        LogError("TryAssignToFaction.ChangeGridOwnership", Scrap);
                    }
                }
            }
            catch (Exception Scrap)
            {
                LogError("TryAssignToFaction.ParseCustomData", Scrap);
            }
        }

        public void DebugWrite(string Source, string Message, string DebugPrefix = "RemoteComponent.")
        {
            Grid.DebugWrite(DebugPrefix + Source, Message);
        }

        public void LogError(string Source, Exception Scrap, string DebugPrefix = "RemoteComponent.")
        {
            Grid.LogError(DebugPrefix + Source, Scrap);
        }

        private void Run()
        {
            try
            {
                if (CanOperate)
                    AI.Main();
                else
                    Shutdown();
            }
            catch (Exception Scrap)
            {
                LogError("Run|AI.Main()", Scrap);
            }
        }

        private void Shutdown(bool Notify = true)
        {
            IsOperable = false;
            try
            {
                if (AI != null && AI.Initialized) AI.Shutdown();
                //(Grid as MyCubeGrid).Editable = true;
            }
            catch (Exception Scrap)
            {
                LogError("Shutdown", Scrap);
            }
            if (Notify) DebugWrite("Shutdown", "RC component shut down.");
        }

        private MyObjectBuilder_EntityBase _builder = null;
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? _builder.Clone() as MyObjectBuilder_EntityBase : _builder;
        }
    }
}