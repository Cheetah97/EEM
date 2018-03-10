using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using EEM.HelperClasses;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Library.Collections;
using static EEM.Constants;

namespace EEM
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public class AiSessionCore : MySessionComponentBase
    {
        private readonly Timer _peaceRequestDelay = new Timer();
        private readonly Timer _factionClock = new Timer();
        private readonly List<long> _newFactionsIDs = new List<long>();
        private bool _inited;
        private bool _factionsInited;

        private static readonly LogWriter DebugLog = new LogWriter(Constants.LogNameAiSessionCore);


        public static bool IsServer => !MyAPIGateway.Multiplayer.MultiplayerActive || MyAPIGateway.Multiplayer.IsServer;

        private static readonly Dictionary<IMyFaction, Dictionary<IMyFaction, DateTime>> PeaceTimers = new Dictionary<IMyFaction, Dictionary<IMyFaction, DateTime>>();

        private static readonly Dictionary<long, BotBase.OnDamageTaken> DamageHandlers = new Dictionary<long, BotBase.OnDamageTaken>();

        private static void AddDamageHandler(long gridId, BotBase.OnDamageTaken handler)
        {
            DamageHandlers.Add(gridId, handler);
        }

        public static void AddDamageHandler(IMyCubeGrid grid, BotBase.OnDamageTaken handler)
        {
            AddDamageHandler(grid.GetTopMostParent().EntityId, handler);
        }

        private static void RemoveDamageHandler(long gridId)
        {
            DamageHandlers.Remove(gridId);
        }

        public static void RemoveDamageHandler(IMyCubeGrid grid)
        {
            RemoveDamageHandler(grid.GetTopMostParent().EntityId);
        }

/*
        private static bool HasDamageHandler(long gridId)
        {
            return DamageHandlers.ContainsKey(gridId);
        }
*/

/*
        private static bool HasDamageHandler(IMyCubeGrid grid)
        {
            return HasDamageHandler(grid.GetTopMostParent().EntityId);
        }
*/

        public override void UpdateBeforeSimulation()
        {
            //if (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) return;
            //MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(0, GenericDamageHandler);
            //MyAPIGateway.Session.DamageSystem.RegisterDestroyHandler(0, GenericDamageHandler);
            //private static void BerforeDamageHandler(object damagedObject, ref MyDamageInformation damage)

            if (_inited) return;

            InitNpcFactions();
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, BeforeDamageHandler);
            MyAPIGateway.Session.DamageSystem.RegisterDestroyHandler(0, DestroyHandler);
            _inited = true;
        }

        private static void BeforeDamageHandler(object damagedObject, ref MyDamageInformation damage)
        {
            GenericDamageHandler(damagedObject, damage);
        }

        private static void DestroyHandler(object damagedObject, MyDamageInformation damage)
        {
            GenericDamageHandler(damagedObject, damage);
        }

        private static void GenericDamageHandler(object damagedObject, MyDamageInformation damage)
        {
            DebugLog.WriteMessage($@"GenericDamageHandler{Tab}damagedObject{Tab}{damagedObject}{Tab}MyDamageInformation(Amount){Tab}{damage.Amount}{Tab}MyDamageInformation(AttackerId){Tab}{damage.AttackerId}");
            try
            {
                if (!(damagedObject is IMySlimBlock)) return;
                IMySlimBlock damagedBlock = (IMySlimBlock) damagedObject;
                IMyCubeGrid damagedGrid = damagedBlock.CubeGrid;
                long gridId = damagedGrid.GetTopMostParent().EntityId;
                if (!DamageHandlers.ContainsKey(gridId)) return;
                try
                {
                    DamageHandlers[gridId].Invoke(damagedBlock, damage);
                }
                catch (Exception scrap)
                {
                    LogError("DamageHandler.Invoke", scrap);
                }
            }
            catch (Exception scrap)
            {
                LogError("GenericDamageHandler", scrap);
            }
        }

        private void InitNpcFactions()
        {
            try
            {
                Diplomacy.Init();
                IMyFactionCollection factionSystem = MyAPIGateway.Session.Factions;

                HashSet<IMyFaction> factionsToMakePeaceWith = Diplomacy.LawfulFactions.ToHashSet();
                factionsToMakePeaceWith.UnionWith(factionSystem.Factions.Values.Where(x => x.IsPlayerFaction()));
                foreach (IMyFaction faction1 in Diplomacy.LawfulFactions)
                {
                    PeaceTimers.Add(faction1, new Dictionary<IMyFaction, DateTime>());
                    foreach (IMyFaction faction2 in factionsToMakePeaceWith)
                    {
                        if (faction1 == faction2) continue;
                        if (faction1.IsPeacefulTo(faction2)) continue;
                        faction1.ProposePeace(faction2, Print: true);
                    }
                }

                _peaceRequestDelay.Interval = 5000;
                _peaceRequestDelay.AutoReset = false;
                _peaceRequestDelay.Elapsed += (trash1, trash2) =>
                {
                    List<IMyFaction> newFactions = new List<IMyFaction>();
                    foreach (long id in _newFactionsIDs)
                        newFactions.Add(factionSystem.TryGetFactionById(id));

                    _newFactionsIDs.Clear();
                    foreach (IMyFaction lawfulFaction in Diplomacy.LawfulFactions)
                    {
                        foreach (IMyFaction newFaction in newFactions)
                            lawfulFaction.ProposePeace(newFaction);
                    }
                    _peaceRequestDelay.Stop();
                };

                factionSystem.FactionCreated += (factionId) =>
                {
                    _newFactionsIDs.Add(factionId);
                    _peaceRequestDelay.Start();
                };

                _factionClock.Interval = 10000;
                _factionClock.AutoReset = true;
                _factionClock.Elapsed += (trash1, trash2) =>
                {
                    if (!_factionsInited)
                    {
                        foreach (IMyFaction faction1 in Diplomacy.LawfulFactions)
                        {
                            foreach (IMyFaction faction2 in Diplomacy.LawfulFactions)
                            {
                                if (faction1 == faction2) continue;
                                if (faction1.IsPeacefulTo(faction2)) continue;
                                faction1.AcceptPeace(faction2);
                            }
                        }
                        _factionsInited = true;
                    }

                    foreach (KeyValuePair<IMyFaction, Dictionary<IMyFaction, DateTime>> npcFactionTimer in PeaceTimers)
                    {
                        IMyFaction npcFaction = npcFactionTimer.Key;
                        List<IMyFaction> remove = new List<IMyFaction>();
                        foreach (KeyValuePair<IMyFaction, DateTime> timer in npcFactionTimer.Value)
                        {
                            IMyFaction faction = timer.Key;
                            if (DateTime.Now >= timer.Value)
                            {
                                npcFaction.ProposePeace(faction, Print: true);
                                remove.Add(faction);
                            }
                        }

                        foreach (IMyFaction faction in remove)
                            npcFactionTimer.Value.Remove(faction);
                    }
                };
                _factionClock.Start();
            }
            catch (Exception scrap)
            {
                LogError("InitNPCFactions", scrap);
            }
        }

        public static void DeclareWar(IMyFaction ownFaction, IMyFaction hostileFaction, TimeSpan truceDelay)
        {
            if (!PeaceTimers.ContainsKey(ownFaction))
            {
                //if (Constants.AllowThrowingErrors) throw new Exception($"PeaceTimers dictionary error: can't find {ownFaction.Tag} key!");
                return;
            }

            DateTime peaceTime = DateTime.Now.Add(truceDelay);

            Dictionary<IMyFaction, DateTime> timerdict = PeaceTimers[ownFaction];

            if (!timerdict.ContainsKey(hostileFaction))
            {
                timerdict.Add(hostileFaction, peaceTime);
            }
            else
            {
                timerdict[hostileFaction] = peaceTime;
            }

            if (!ownFaction.IsHostileTo(hostileFaction))
            {
                ownFaction.DeclareWar(hostileFaction, Print: true);
            }

            DebugWrite($"DeclareWarTimer[{ownFaction.Tag}]", $"Added peace timer. Current time: {DateTime.Now:HH:mm:ss} | Calmdown at: {peaceTime:HH:mm:ss} | Calmdown delay: {truceDelay.ToString()}");
        }

        public static void LogError(string source, Exception scrap, string debugPrefix = "SessionCore.")
        {
            DebugHelper.Print("SessionCore", $"Fatal error in '{debugPrefix + source}': {scrap.Message}. {(scrap.InnerException != null ? scrap.InnerException.Message : "No additional info was given by the game :(")}");
        }

        public static void DebugWrite(string source, string message, bool antiSpam = true)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable once HeuristicUnreachableCode
            #pragma warning disable 162
            if (Constants.GlobalDebugMode) DebugHelper.Print($"{source}", $"{message}", antiSpam);
            #pragma warning restore 162
        }
    }
}