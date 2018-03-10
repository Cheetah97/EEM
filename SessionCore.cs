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
using VRage.Collections;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Interfaces;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace Cheetah.AI
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AISessionCore : MySessionComponentBase
    {
        #region Settings
        // Here are the options which can be freely edited.

        /// <summary>
        /// This toggles the Debug mode. Without Debug, only critical messages are shown in chat.
        /// </summary>
        public static readonly bool Debug = false;

        /// <summary>
        /// This toggles the Debug Anti-Spam mode. With this, a single combination of ship name + message will be displayed only once per session.
        /// </summary>
        public static readonly bool DebugAntiSpam = false;
        #endregion




        public const string WARPIN_EFFECT = "EEMWarpIn";
        Timer PeaceRequestDelay = new Timer();
        Timer FactionClock = new Timer();
        List<long> NewFactionsIDs = new List<long>();

        /// <summary>
        /// This permits certain operations to throw custom exceptions in order to
        /// provide detailed descriptions of what gone wrong, over call stack.<para />
        /// BEWARE, every exception thrown must be explicitly provided with a catcher, or it will crash the entire game!
        /// </summary>
        public static readonly bool AllowThrowingErrors = true;

        public static bool IsServer
        {
            get
            {
                return !MyAPIGateway.Multiplayer.MultiplayerActive || MyAPIGateway.Multiplayer.IsServer;
            }
        }

        private static Dictionary<long, BotBase.OnDamageTaken> DamageHandlers = new Dictionary<long, BotBase.OnDamageTaken>();
        #region DictionaryAccessors
        static public void AddDamageHandler(long GridID, BotBase.OnDamageTaken Handler)
        {
            DamageHandlers.Add(GridID, Handler);
        }
        static public void AddDamageHandler(IMyCubeGrid Grid, BotBase.OnDamageTaken Handler)
        {
            AddDamageHandler(Grid.GetTopMostParent().EntityId, Handler);
        }
        static public void RemoveDamageHandler(long GridID)
        {
            DamageHandlers.Remove(GridID);
        }
        static public void RemoveDamageHandler(IMyCubeGrid Grid)
        {
            RemoveDamageHandler(Grid.GetTopMostParent().EntityId);
        }
        static public bool HasDamageHandler(long GridID)
        {
            return DamageHandlers.ContainsKey(GridID);
        }
        static public bool HasDamageHandler(IMyCubeGrid Grid)
        {
            return HasDamageHandler(Grid.GetTopMostParent().EntityId);
        }
        #endregion

        bool Inited = false;
        bool FactionsInited = false;

        public override void UpdateBeforeSimulation()
        {
            //if (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) return;
            if (Inited) return;

            InitNPCFactions();
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, DamageRefHandler);
            //MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(0, GenericDamageHandler);
            MyAPIGateway.Session.DamageSystem.RegisterDestroyHandler(0, GenericDamageHandler);
            Inited = true;
        }

        public void DamageRefHandler(object DamagedObject, ref MyDamageInformation Damage)
        {
            GenericDamageHandler(DamagedObject, Damage);
        }

        public void GenericDamageHandler(object DamagedObject, MyDamageInformation Damage)
        {
            try
            {
                if (DamagedObject == null) return;
                if (!(DamagedObject is IMySlimBlock)) return;
                IMySlimBlock DamagedBlock = DamagedObject as IMySlimBlock;
                IMyCubeGrid DamagedGrid = DamagedBlock.CubeGrid;
                long GridID = DamagedGrid.GetTopMostParent().EntityId;
                if (DamageHandlers.ContainsKey(GridID))
                {
                    try
                    {
                        DamageHandlers[GridID].Invoke(DamagedBlock, Damage);
                    }
                    catch (Exception Scrap)
                    {
                        LogError("DamageHandler.Invoke", Scrap);
                    }
                }
            }
            catch (Exception Scrap)
            {
                LogError("GenericDamageHandler", Scrap);
            }
        }

        private void InitNPCFactions()
        {
            try
            {
                Diplomacy.Init();
                var FactionSystem = MyAPIGateway.Session.Factions;

                HashSet<IMyFaction> FactionsToMakePeaceWith = Diplomacy.LawfulFactions.ToHashSet();
                FactionsToMakePeaceWith.UnionWith(FactionSystem.Factions.Values.Where(x => x.IsPlayerFaction()));
                foreach (var Faction1 in Diplomacy.LawfulFactions)
                {
                    PeaceTimers.Add(Faction1, new Dictionary<IMyFaction, DateTime>());
                    foreach (var Faction2 in FactionsToMakePeaceWith)
                    {
                        if (Faction1 == Faction2) continue;
                        if (Faction1.IsPeacefulTo(Faction2)) continue;
                        Faction1.ProposePeace(Faction2, Print: true);
                    }
                }
                
                #region Peace Request Delay
                PeaceRequestDelay.Interval = 5000;
                PeaceRequestDelay.AutoReset = false;
                PeaceRequestDelay.Elapsed += (trash1, trash2) =>
                {
                    List<IMyFaction> NewFactions = new List<IMyFaction>();
                    foreach (long ID in NewFactionsIDs)
                        NewFactions.Add(FactionSystem.TryGetFactionById(ID));

                    NewFactionsIDs.Clear();
                    foreach (IMyFaction LawfulFaction in Diplomacy.LawfulFactions)
                    {
                        foreach (IMyFaction NewFaction in NewFactions)
                            LawfulFaction.ProposePeace(NewFaction);
                    }
                    PeaceRequestDelay.Stop();
                };

                FactionSystem.FactionCreated += (FactionID) =>
                {
                    NewFactionsIDs.Add(FactionID);
                    PeaceRequestDelay.Start();
                };
                #endregion

                #region Faction Clock
                FactionClock.Interval = 10000;
                FactionClock.AutoReset = true;
                FactionClock.Elapsed += (trash1, trash2) =>
                {
                    if (!FactionsInited)
                    {
                        foreach (var Faction1 in Diplomacy.LawfulFactions)
                        {
                            foreach (var Faction2 in Diplomacy.LawfulFactions)
                            {
                                if (Faction1 == Faction2) continue;
                                if (Faction1.IsPeacefulTo(Faction2)) continue;
                                Faction1.AcceptPeace(Faction2);
                            }
                        }
                        FactionsInited = true;
                    }

                    foreach (var NPCFactionTimer in PeaceTimers)
                    {
                        var NPCFaction = NPCFactionTimer.Key;
                        List<IMyFaction> remove = new List<IMyFaction>();
                        foreach (var Timer in NPCFactionTimer.Value)
                        {
                            var Faction = Timer.Key;
                            if (DateTime.Now >= Timer.Value)
                            {
                                NPCFaction.ProposePeace(Faction, Print: true);
                                remove.Add(Faction);
                            }
                        }

                        foreach (var Faction in remove)
                            NPCFactionTimer.Value.Remove(Faction);
                    }
                };
                FactionClock.Start();
                #endregion
            }
            catch (Exception Scrap)
            {
                LogError("InitNPCFactions", Scrap);
            }
        }

        public static Dictionary<IMyFaction, Dictionary<IMyFaction, DateTime>> PeaceTimers = new Dictionary<IMyFaction, Dictionary<IMyFaction, DateTime>>();
        public static void DeclareWar(IMyFaction OwnFaction, IMyFaction HostileFaction, TimeSpan TruceDelay)
        {
            if (!PeaceTimers.ContainsKey(OwnFaction))
            {
                if (AllowThrowingErrors) throw new Exception($"PeaceTimers dictionary error: can't find {OwnFaction.Tag} key!");
                return;
            }

            DateTime PeaceTime = DateTime.Now.Add(TruceDelay);

            var timerdict = PeaceTimers[OwnFaction];

            if (!timerdict.ContainsKey(HostileFaction))
            {
                timerdict.Add(HostileFaction, PeaceTime);
            }
            else
            {
                timerdict[HostileFaction] = PeaceTime;
            }

            if (!OwnFaction.IsHostileTo(HostileFaction))
            {
                OwnFaction.DeclareWar(HostileFaction, Print: true);
            }

            DebugWrite($"DeclareWarTimer[{OwnFaction.Tag}]", $"Added peace timer. Current time: {DateTime.Now.ToString("HH:mm:ss")} | Calmdown at: {PeaceTime.ToString("HH:mm:ss")} | Calmdown delay: {TruceDelay.ToString()}");
        }

        public static void LogError(string Source, Exception Scrap, string DebugPrefix = "SessionCore.")
        {
            DebugHelper.Print("SessionCore", $"Fatal error in '{DebugPrefix + Source}': {Scrap.Message}. {(Scrap.InnerException != null ? Scrap.InnerException.Message : "No additional info was given by the game :(")}");
        }

        public static void DebugWrite(string Source, string Message, bool AntiSpam = true)
        {
            if (Debug) DebugHelper.Print($"{Source}", $"{Message}", AntiSpam);
        }
    }
}