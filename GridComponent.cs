using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using EEM.HelperClasses;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace EEM
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), true)]
    // ReSharper disable once UnusedMember.Global
    public class GridComponent : MyGameLogicComponent
    {
        private IMyRemoteControl _remoteControl;

        private IMyCubeGrid Grid { get; set; }

        private BotBase _ai;

        private bool _isOperable;

        private bool CanOperate
        {
            get
            {
                try
                {
                    if (_ai == null)
                    {
                        //DebugWrite("CanOperate", "AI is null");
                        return false;
                    }
                    return _isOperable && Grid.InScene && _ai.Operable;
                }
                catch (Exception scrap)
                {
                    //DebugWrite("CanOperate", $"Grid {(Grid != null ? "!=" : "==")} null; AI {(AI != null ? "!=" : "==")} null");
                    LogError("CanOperate", scrap);
                    return false;
                }
            }
        }

        /// <summary>
        /// Provides a simple way to recompile all PBs on the grid, with given delay.
        /// </summary>
        private readonly Timer _recompileDelay = new Timer(500);
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _builder = objectBuilder;
            _remoteControl = Entity as IMyRemoteControl;
            if (_remoteControl == null) return;
            Grid = _remoteControl.CubeGrid.GetTopMostParent() as IMyCubeGrid;
            //MyAPIGateway.Utilities.ShowMessage($"{Grid.DisplayName}", $"RC component inited");
            _remoteControl.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME |
                              MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        private void SetupRecompileTimer()
        {
            _recompileDelay.AutoReset = false;
            _recompileDelay.Elapsed += (trash1, trash2) =>
            {
                _recompileDelay.Stop();
                //RecompilePBs();
            };
        }

        private static List<IMyProgrammableBlock> ProgrammableBlockCollection { get; set; }

        private void RecompilePBs()
        {
            foreach (IMyProgrammableBlock programmableBlock in Grid.GetTerminalSystem().GetBlocksOfType<IMyProgrammableBlock>())
            {
                programmableBlock.Recompile();
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!_inited) InitAi();
        }

        // The grid component's updating is governed by AI.Update, and the functions are called as flagged in update.
        public override void UpdateBeforeSimulation() { if (CanOperate) Run(); }
        public override void UpdateBeforeSimulation10() { if (CanOperate) Run(); }
        public override void UpdateBeforeSimulation100() { if (CanOperate) Run(); }

        private bool _inited;

        private void InitAi()
        {
            if (!AiSessionCore.IsServer) return;
            SetupRecompileTimer();

            try
            {
                if (Grid.Physics == null || ((MyEntity) Grid).IsPreview) return;
            }
            catch (Exception scrap)
            {
                LogError("InitAI[grid check]", scrap);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(_remoteControl.CustomData) || !_remoteControl.CustomData.Contains("[EEM_AI]"))
                {
                    Shutdown(notify: false);
                    return;
                }

                //DebugWrite("GridComponent.InitAI", "Booting up RC component.");

                if (!_remoteControl.IsOwnedByNPC())
                {
                    DebugWrite("GridComponent.InitAI", "RC is not owned by NPC!");
                    Shutdown(notify: false);
                    return;
                }

                if (_remoteControl.CustomData.Contains("Faction:"))
                {
                    try
                    {
                        TryAssignToFaction();
                    }
                    catch(Exception scrap)
                    {
                        LogError("TryAssignToFaction", scrap);
                    }
                }

                if (_remoteControl.CustomData.Contains("Type:None"))
                {
                    _isOperable = true;
                    _inited = true;
                    DebugWrite("GridComponent.InitAI", "Type:None, shutting down.");
                    Shutdown(notify: false);
                    return;
                }

                BotTypes botType = BotBase.ReadBotType(_remoteControl);
                if (botType == BotTypes.None)
                {
                    DebugWrite("GridComponent.InitAI", "Skipping grid — no setup found");
                }
                else if (botType == BotTypes.Invalid)
                {
                    LogError("GridComponent.InitAI", new Exception("Bot type is not valid!", new Exception()));
                }

                //DebugWrite("GridComponent.InitAI", $"Bot found. Bot type: {BotType.ToString()}");
            }
            catch (Exception scrap)
            {
                LogError("GridComponent.InitAI", scrap);
                return;
            }

            try
            {
                _ai = BotFabrication.FabricateBot(Grid, _remoteControl);

                if (_ai == null)
                {
                    DebugWrite("GridComponent.InitAI", "Bot Fabricator yielded null");
                    Shutdown();
                    return;
                }

                bool init = _ai.Init(_remoteControl);

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

                if (_ai.Update != default(MyEntityUpdateEnum)) _remoteControl.NeedsUpdate |= _ai.Update;
                _remoteControl.OnMarkForClose += (trash) => { Shutdown(); };
                _isOperable = true;
                _inited = true;
                RecompilePBs();
                //Grid.DebugWrite("InitAI", "Grid Component successfully inited.");
            }
            catch (Exception scrap)
            {
                LogError("GridComponent.InitAI", scrap);
                Shutdown();
            }
        }

        private void TryAssignToFaction()
        {
            try
            {
                if (!AiSessionCore.IsServer) return;
                if (string.IsNullOrWhiteSpace(_remoteControl.CustomData)) return;

                string customData = _remoteControl.CustomData.Replace("\r\n", "\n");
                if (!_remoteControl.CustomData.Contains("Faction:")) return;

                List<string> split = customData.Split('\n').Where(x => x.Contains("Faction:")).ToList();
                if (!split.Any()) return;
                string factionLine = split[0].Trim();
                string[] lineSplit = factionLine.Split(':');
                if (lineSplit.Length != 2)
                {
                    Grid.LogError("TryAssignToFaction", new Exception("Cannot assign to faction", new Exception($"Line '{factionLine}' cannot be parsed.")));
                    return;
                }
                string factionTag = lineSplit[1].Trim();

                if (factionTag == "Nobody")
                {
                    Grid.ChangeOwnershipSmart(0, MyOwnershipShareModeEnum.All);
                    _recompileDelay.Start();
                    //DebugWrite("TryAssignToFaction", $"Assigned to nobody, recompiled scripts");
                }
                else
                {
                    IMyFaction faction = MyAPIGateway.Session.Factions.Factions.Values.FirstOrDefault(x => x.Tag == factionTag);

                    if (faction == null)
                    {
                        Grid.LogError("TryAssignToFaction", new Exception($"Faction with tag '{factionTag}' was not found!"));
                        return;
                    }

                    try
                    {
                        Grid.ChangeOwnershipSmart(faction.FounderId, MyOwnershipShareModeEnum.Faction);
                        //DebugWrite("TryAssignToFaction", $"Assigned to faction '{Data[1]}', recompiled scripts");
                    }
                    catch (Exception scrap)
                    {
                        LogError("TryAssignToFaction.ChangeGridOwnership", scrap);
                    }
                }
            }
            catch (Exception scrap)
            {
                LogError("TryAssignToFaction.ParseCustomData", scrap);
            }
        }

        private void DebugWrite(string source, string message, string debugPrefix = "RemoteComponent.")
        {
            Grid.DebugWrite(debugPrefix + source, message);
        }

        private void LogError(string source, Exception scrap, string debugPrefix = "RemoteComponent.")
        {
            Grid.LogError(debugPrefix + source, scrap);
        }

        private void Run()
        {
            try
            {
                if (CanOperate)
                    _ai.Main();
                else
                    Shutdown();
            }
            catch (Exception scrap)
            {
                LogError("Run|AI.Main()", scrap);
            }
        }

        private void Shutdown(bool notify = true)
        {
            _isOperable = false;
            try
            {
                if (_ai != null && _ai.Initialized) _ai.Shutdown();
                //(Grid as MyCubeGrid).Editable = true;
            }
            catch (Exception scrap)
            {
                LogError("Shutdown", scrap);
            }
            if (notify) DebugWrite("Shutdown", "RC component shut down.");
        }

        private MyObjectBuilder_EntityBase _builder;
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? _builder.Clone() as MyObjectBuilder_EntityBase : _builder;
        }
    }
}