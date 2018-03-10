using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EEM.HelperClasses;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using Sandbox.ModAPI.Weapons;

namespace EEM
{
    /// <summary>
    /// This class will handle bot's diplomatic relations, or, simpler, going hostile when messed with.
    /// </summary>
    public class BotDiplomacyHandler
    {
        private static readonly LogWriter DebugLog = new LogWriter(Constants.LogNameDamageHelper);
        public BotBase MyAI { get; private set; }
        protected IMyCubeGrid Grid => MyAI.Grid;
        public IMyFaction OwnerFaction { get; private set; }
        protected readonly TimeSpan PeaceDelay;
        //private Action<IMyPlayer> AdditionalGoingHostileHandler;

        public BotDiplomacyHandler(BotBase AI, /*Action<IMyPlayer> AdditionalHandler = null,*/ TimeSpan? PeaceDelay = null)
        {
            MyAI = AI;
            this.PeaceDelay = PeaceDelay.HasValue ? PeaceDelay.Value : TimeSpan.FromMinutes(15);
            //AdditionalGoingHostileHandler = AdditionalHandler;
        }

        public void GoHostile(IMyFaction Faction)
        {
            try
            {
                AiSessionCore.DeclareWar(OwnerFaction, Faction, PeaceDelay);
                if (OwnerFaction.IsLawful())
                {
                    AiSessionCore.DeclareWar(Diplomacy.Police, Faction, PeaceDelay);
                    AiSessionCore.DeclareWar(Diplomacy.Army, Faction, PeaceDelay);
                }
            }
            catch (Exception scrap)
            {
                Grid.LogError("GoHostile[faction]", scrap);
            }
        }

        public void GoHostile(IMyPlayer player)
        {
            try
            {
                if (player == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: Damager is null.");
                    return;
                }

                if (OwnerFaction == null)
                {
                    OwnerFaction = Grid.GetOwnerFaction();
                }

                if (OwnerFaction == null || !OwnerFaction.IsNPC())
                {
                    Grid.DebugWrite("RegisterHostileAction", $"Error: {(OwnerFaction == null ? "can't find own faction" : "own faction isn't recognized as NPC.")}");
                    return;
                }

                IMyFaction hostileFaction = player.GetFaction();
                if (hostileFaction == null)
                {
                    Grid.DebugWrite("RegisterHostileAction", "Error: can't find damager's faction");
                    return;
                }
                else if (hostileFaction == OwnerFaction)
                {
                    OwnerFaction.Kick(player);
                    return;
                }
                GoHostile(hostileFaction);
            }
            catch (Exception scrap)
            {
                Grid.LogError("GoHostile[player]", scrap);
            }
        }

        /// <summary>
        /// Placeholder, just in case we'll add anything which would require closing
        /// </summary>
        public void Close() { }
    }
}
